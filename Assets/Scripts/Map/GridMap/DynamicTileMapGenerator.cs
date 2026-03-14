using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Grid))]
public class DynamicTileMapGenerator : MonoBehaviour
{
    #region 面版属性

    [Header("地图设置")]
    [Range(1, 100)][Tooltip("总地图大小边长限制")] public int totalChunksLengthLimit = 10;
    [Tooltip("总地图坐标限制")] public Vector2Int[] Bottomlefttotopright = new Vector2Int[2];


    [Header("Chunk设置")]
    public int singleChunkWidth = 20;
    public int singleChunkHeight = 20;
    [Tooltip("玩家周围预生成的Chunk数量（半径）")] public int preGenerateRadius = 1;
    [Tooltip("超过此距离的Chunk会被卸载")] public int unloadDistance = 2;


    [Header("Perlin Noise参数设置")]
    public float noiseScale = 20f;
    [Range(5, 8)][Tooltip("差异邻接剔除")] public int difCutOffNum = 5;
    [Range(1, 6)][Tooltip("采样层数")] public int octaves = 4;
    [Range(0, 1)][Tooltip("振幅衰减，下层噪声比上层衰减的倍率")] public float persistence = 0.5f;
    [Range(2, 4)][Tooltip("频程倍率，下层噪声频率比上层增加的倍数")] public float lacunarity = 2f;
    [Tooltip("噪声采样偏移")] public Vector2 offset = Vector2.zero;
    [Tooltip("随机种子")] public int seed = 0;


    [Header("瓦片集和分类阈值（按高度降序）")]
    public List<CustomTile> terrainTiles;
    public List<float> heightThresholds;


    [Header("过渡瓦片集")]
    public List<BoundaryRule> boundaryRules;


    [Header("地图层")]
    public Tilemap groundTilemap;
    public Tilemap transitionBackgroundTilemap;
    public Tilemap transitionTilemap;

    #endregion

    private NoiseGenerator noiseGenerator;
    private TerrainTileResolver terrainTileResolver;
    private ChunkManager chunkManager;
    private TilemapRender tilemapRender;
    private TileTransitionGenerator tileTransitionGenerator;
    private Vector2Int chunkSize;

    private void Awake()
    {
        chunkSize = new Vector2Int(singleChunkWidth, singleChunkHeight);

        noiseGenerator = new NoiseGenerator(
            noiseScale,
            seed,
            octaves,
            persistence,
            lacunarity,
            offset,
            chunkSize
        );

        terrainTileResolver = new TerrainTileResolver(
            terrainTiles,
            heightThresholds
        );

        tileTransitionGenerator = new TileTransitionGenerator(
            boundaryRules,
            terrainTileResolver,
            difCutOffNum
        );

        tilemapRender = new TilemapRender(
            groundTilemap,
            transitionBackgroundTilemap,
            transitionTilemap
        );

        chunkManager = new ChunkManager(
            noiseGenerator,
            terrainTileResolver,
            tileTransitionGenerator,
            tilemapRender,
            chunkSize
        );
    }
    void Start()
    {
        CheckAndGenerateSurroundChunks(Vector2Int.zero);
    }

    void Update()
    {
        // if (PlayerManager.Instance == null || Camera.main == null)
        //     return;

        // // 获取玩家当前所在Chunk
        // Vector2Int playerChunkIndex = GetPlayerCurrentChunkIndex();

        // // 生成周围Chunk
        // ChunkManager.CheckAndGenerateSurroundChunks(playerChunkIndex);

        // // 卸载过远的Chunk
        // if (unloadDistance > preGenerateRadius)
        //     UnloadDistantChunks(playerChunkIndex);
    }
    private void OnValidate()
    {
        // 确保地图坐标不超出大小限制
        while (Bottomlefttotopright[1].x < Bottomlefttotopright[0].x)
        {
            Bottomlefttotopright[1].x = Bottomlefttotopright[0].x + 1;
        }
        while (Bottomlefttotopright[1].y < Bottomlefttotopright[0].y)
        {
            Bottomlefttotopright[1].y = Bottomlefttotopright[0].y + 1;
        }
        while ((Bottomlefttotopright[1].x - Bottomlefttotopright[0].x) > totalChunksLengthLimit)
        {
            Bottomlefttotopright[1].x = Bottomlefttotopright[0].x + totalChunksLengthLimit;
        }
        while ((Bottomlefttotopright[1].y - Bottomlefttotopright[0].y) > totalChunksLengthLimit)
        {
            Bottomlefttotopright[1].y = Bottomlefttotopright[0].y + totalChunksLengthLimit;
        }


        // 确保阈值数量与瓦片数量匹配
        if (terrainTiles != null && heightThresholds != null)
        {
            while (heightThresholds.Count >= terrainTiles.Count)
                heightThresholds.RemoveAt(heightThresholds.Count - 1);

            while (heightThresholds.Count < terrainTiles.Count - 1)
                heightThresholds.Add(heightThresholds.Count > 0 ? heightThresholds[heightThresholds.Count - 1] + 0.1f : 0);
        }

        // 确保卸载距离大于预生成半径
        if (unloadDistance <= preGenerateRadius)
            unloadDistance = 2 * preGenerateRadius;
    }

    private bool WithinLimits(Vector2Int target, Vector2Int minVector, Vector2Int maxVector)
    {
        return IsInRange(target.x, minVector.x, maxVector.x) && IsInRange(target.y, minVector.y, maxVector.y);
    }
    private bool IsInRange(int x, int a, int b)
    {
        if (a == b)
            return false;
        if (a > b)
        {
            (b, a) = (a, b);
        }
        if (x < a || x > b)
            return false;

        return true;
    }

    /// <summary>
    /// 检查并生成玩家周围的Chunk
    /// </summary>
    private void CheckAndGenerateSurroundChunks(Vector2Int centerChunkIndex)
    {
        // 生成以玩家为中心，指定半径内的所有Chunk
        for (int y = -2 * preGenerateRadius; y <= 2 * preGenerateRadius; y++)
        {
            for (int x = -2 * preGenerateRadius; x <= 2 * preGenerateRadius; x++)
            {
                if (math.abs(x) + math.abs(y) <= 2 * preGenerateRadius)
                {
                    Vector2Int targetChunkIndex = centerChunkIndex + new Vector2Int(x, y);

                    if (WithinLimits(targetChunkIndex, Bottomlefttotopright[0], Bottomlefttotopright[1]))
                    {
                        chunkManager.GenerateChunk(targetChunkIndex);
                    }
                    else continue;
                }

            }
        }
    }
}
