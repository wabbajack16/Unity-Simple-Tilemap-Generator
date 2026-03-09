using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.VisualScripting;
using System;
using System.Linq;
using System.Drawing;

[RequireComponent(typeof(Grid))]
public class DynamicMapGenerator : MonoBehaviour
{
    #region 属性

    [Header("地图设置")]
    [Range(1, 100)][Tooltip("总地图大小边长限制")] public int totalChunksLengthLimit = 10;
    [Tooltip("总地图坐标限制")] public Vector2Int[] Bottomlefttotopright = new Vector2Int[2];
    public int singleChunkWidth = 20;
    public int singleChunkHeight = 20;
    public float noiseScale = 20f;
    [Range(5, 8)][Tooltip("差异邻接剔除")] public int difCutOffNum = 5;
    [Range(1, 6)][Tooltip("采样层数")] public int octaves = 4;
    [Range(0, 1)][Tooltip("振幅衰减，下层噪声比上层衰减的倍率")] public float persistence = 0.5f;
    [Range(2, 4)][Tooltip("频程倍率，下层噪声频率比上层增加的倍数")] public float lacunarity = 2f;
    [Tooltip("噪声采样偏移")] public Vector2 offset = Vector2.zero;
    [Tooltip("随机种子")] public int seed = 0;
    [Tooltip("玩家周围预生成的Chunk数量（半径）")] public int preGenerateRadius = 1;
    [Tooltip("超过此距离的Chunk会被卸载")] public int unloadDistance = 2;


    [Header("瓦片集和分类阈值（按高度降序）")]
    public List<CustomTile> terrainTiles;
    public List<float>      heightThresholds;


    [Header("过渡瓦片集")]
    public List<BoundaryRule> boundaryRules;


    [Header("地图层")]
    public Tilemap groundTilemap;
    public Tilemap transitionBackgroundTilemap;
    public Tilemap transitionTilemap;


    // 瓦片数据缓存
    /// <summary>
    /// z = 0 <see langword="baseTile"/>;
    /// z = 1 <see langword="transitionBG"/>;
    /// z = 2 <see langword="transition"/>
    /// </summary>
    private readonly Dictionary<Vector3Int, TileType>               _baseTileDict = new();          // 基础瓦片
    private readonly Dictionary<(TileType, TileType), BoundaryRule> _boundaryRuleDict = new();      // 过渡瓦片集
    private readonly Dictionary<TileType, CustomTile>               _tileTypeToCustomTile = new();  // 瓦片类型到资源的映射
    private readonly Dictionary<Vector3Int, int>                    _dirToIndexDict = new();        // 方向向量到索引的映射
    private List<BoundaryRule>                                      tempMatchedRules;               // 临时匹配规则
    private readonly Dictionary<Vector3Int, CustomTile>[] _setTileCaches = new Dictionary<Vector3Int, CustomTile>[3];

    // Chunk管理
    private struct ChunkCacheData
    {
        public int startX;
        public int startY;
        public int endX;
        public int endY;
        public float lastAccessTime;
    }
    private Dictionary<Vector2Int, ChunkCacheData> _ChunkCacheDict = new();
    private Vector2Int _chunkSize;

    // 地图边界
    private int _minX = int.MaxValue;
    private int _maxX = int.MinValue;
    private int _minY = int.MaxValue;
    private int _maxY = int.MinValue;

    /// <summary>
    /// 方向数组：上、下、左、右、上右、下右、上左、下左
    /// </summary>
    private readonly int[] _dirX = { 0, 0, -1, 1, 1, 1, -1, -1 };
    private readonly int[] _dirY = { 1, -1, 0, 0, 1, -1, 1, -1 };

    // 临时变量（避免频繁创建）
    private BoundaryRule    finalRule;
    private CustomTile      tileToSet;
    private TileType        baseTileType;
    private TileType        newType;
    private readonly List<Vector2Int>   chunksToUnload = new();

    #endregion


    #region 事件函数

    private void Awake()
    {
        // 初始化Tilemap引用
        if (groundTilemap == null)
            groundTilemap = transform.GetChild(0).GetComponent<Tilemap>();
        if (transitionTilemap == null)
            transitionTilemap = transform.GetChild(1).GetComponent<Tilemap>();

        // 初始化缓存
        InitCaches();

        // 初始化Chunk尺寸
        _chunkSize = new Vector2Int(singleChunkWidth, singleChunkHeight);
    }

    private void Start()
    {
        // // 生成初始Chunk（玩家周围）
        // if (PlayerManager.Instance != null)
        // {
        //     Vector2Int initialChunkIndex = GetPlayerCurrentChunkIndex();
        //     CheckAndGenerateSurroundChunks(initialChunkIndex);
        // }
        CheckAndGenerateSurroundChunks(Vector2Int.zero);
    }

    private void Update()
    {
        // if (PlayerManager.Instance == null || Camera.main == null)
        //     return;

        // // 获取玩家当前所在Chunk
        // Vector2Int playerChunkIndex = GetPlayerCurrentChunkIndex();

        // // 生成周围Chunk
        // CheckAndGenerateSurroundChunks(playerChunkIndex);

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
            unloadDistance = 2 *preGenerateRadius;
    }

    #endregion


    #region 初始化chunk、缓存和噪声图

    /// <summary>
    /// 初始化所有缓存数据
    /// </summary>
    private void InitCaches()
    {
        // 过渡规则缓存
        if (boundaryRules != null)
        {
            foreach (var rule in boundaryRules)
            {
                var key = (rule.sourceType, rule.adjacentType);
                if (!_boundaryRuleDict.ContainsKey(key))
                    _boundaryRuleDict.Add(key, rule);
            }
        }

        // 瓦片类型到资源的映射
        if (terrainTiles != null)
        {
            foreach (var tile in terrainTiles)
            {
                if (tile.type != TileType.None && !_tileTypeToCustomTile.ContainsKey(tile.type))
                    _tileTypeToCustomTile.Add(tile.type, tile);
            }
        }

        // 方向向量到索引的映射
        for (int i = 0; i < _dirX.Length; i++)
        {
            var dir = new Vector3Int(_dirX[i], _dirY[i], 0);
            if (!_dirToIndexDict.ContainsKey(dir))
                _dirToIndexDict.Add(dir, i);
        }

        for (int i = 0; i < _setTileCaches.Length; i++)
        {
            _setTileCaches[i] = new Dictionary<Vector3Int, CustomTile>();
        }

        // 临时列表初始化
            tempMatchedRules = new List<BoundaryRule>();
    }

    // /// <summary>
    // /// 获取玩家当前所在的Chunk索引
    // /// </summary>
    // private Vector2Int GetPlayerCurrentChunkIndex()
    // {
    //     Vector3 playerPos = PlayerManager.Instance.transform.position;
    //     int chunkX = Mathf.FloorToInt(playerPos.x / _chunkSize.x);
    //     int chunkY = Mathf.FloorToInt(playerPos.y / _chunkSize.y);
    //     return new Vector2Int(chunkX, chunkY);
    // }

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
                if (Math.Abs(x) + math.abs(y) <= 2 * preGenerateRadius)
                {
                    Vector2Int targetChunkIndex = centerChunkIndex + new Vector2Int(x, y);

                    if (WithinLimits(targetChunkIndex, Bottomlefttotopright[0], Bottomlefttotopright[1]))
                    {
                            GenerateChunkIfNotExists(targetChunkIndex);
                    }
                    else continue;
                }

            }
        }
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
    /// 缓存查找
    /// </summary>
    private bool IsBaseTileCached(Vector2Int chunkIndex, out int startX, out int startY, out int endX, out int endY)
    {
        // 计算Chunk的世界坐标范围
        startX = chunkIndex.x * _chunkSize.x;
        startY = chunkIndex.y * _chunkSize.y;
        endX = startX + _chunkSize.x - 1;
        endY = startY + _chunkSize.y - 1;

        var checkPoints = new List<Vector3Int>
        {
            new(startX, startY, 0), // 左下角
            new(startX, endY, 0),   // 左上角
            new(endX, endY, 0),     // 右上角
            new(endX, startY, 0),   // 右上角
            new((startX + endX)/2, (startY + endY)/2, 0) // 中心
        };

        foreach (var pos in checkPoints)
        {
            if (!_baseTileDict.ContainsKey(pos))
                return false;
        }
        return true;
    }

    /// <summary>
    /// 如果Chunk不存在则生成它
    /// </summary>
    private void GenerateChunkIfNotExists(Vector2Int chunkIndex)
    {
        if (_ChunkCacheDict.ContainsKey(chunkIndex))
        {
            var cacheData = _ChunkCacheDict[chunkIndex];
            cacheData.lastAccessTime = Time.time;
            _ChunkCacheDict[chunkIndex] = cacheData;
            return;
        }

        if (IsBaseTileCached(chunkIndex, out var chunkStartX, out var chunkStartY, out var chunkEndX, out var chunkEndY))
            GenerateBaseMap(chunkStartX, chunkStartY, chunkEndX, chunkEndY);
        else
        {
            // 生成噪声图
            using (var noiseArray = GenerateNoiseMapParallel(chunkStartX, chunkStartY))
            {
                GenerateBaseMap(chunkStartX, chunkStartY, chunkEndX, chunkEndY, noiseArray);
            }
        }

        // 更新地图边界
        UpdateMapBounds(chunkStartX, chunkEndX, chunkStartY, chunkEndY);

        // 标记为已生成
        _ChunkCacheDict.Add(chunkIndex, new ChunkCacheData
        {
            startX = chunkStartX,
            startY = chunkStartY,
            endX = chunkEndX,
            endY = chunkEndY,
            lastAccessTime = Time.time

        });

        // 生成过渡瓦片
        GenerateChunkTransitionTiles(chunkStartX, chunkEndX, chunkStartY, chunkEndY);

        groundTilemap.SetTiles(_setTileCaches[0].Keys.ToArray(), _setTileCaches[0].Values.ToArray());
        transitionBackgroundTilemap.SetTiles(_setTileCaches[1].Keys.ToArray(), _setTileCaches[1].Values.ToArray());
        transitionTilemap.SetTiles(_setTileCaches[2].Keys.ToArray(), _setTileCaches[2].Values.ToArray());

        _setTileCaches[0].Clear();
        _setTileCaches[1].Clear();
        _setTileCaches[2].Clear();

        // 调试信息
        // Debug.Log($"生成Chunk: {chunkKey} ({chunkStartX}~{chunkEndX}, {chunkStartY}~{chunkEndY})");
    }


    /// <summary>
    /// 更新地图边界
    /// </summary>
    private void UpdateMapBounds(int newMinX, int newMaxX, int newMinY, int newMaxY)
    {
        _minX = Mathf.Min(_minX, newMinX);
        _maxX = Mathf.Max(_maxX, newMaxX);
        _minY = Mathf.Min(_minY, newMinY);
        _maxY = Mathf.Max(_maxY, newMaxY);
    }

    /// <summary>
    /// 并行生成噪声图
    /// </summary>
    private NativeArray<float> GenerateNoiseMapParallel(int chunkStartX, int chunkStartY)
    {
        var noiseArray = new NativeArray<float>(_chunkSize.x * _chunkSize.y, Allocator.TempJob);
        var prng = new System.Random(seed);

        var octaveOffsets = new NativeArray<Vector2>(octaves, Allocator.TempJob);
        for (int i = 0; i < octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) + offset.x;
            float offsetY = prng.Next(-100000, 100000) + offset.y;
            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        }

        var noiseJob = new NoiseCalculationJob
        {
            ChunkWidth = _chunkSize.x,
            ChunkHeight = _chunkSize.y,
            NoiseScale = noiseScale,
            Octaves = octaves,
            Persistence = persistence,
            Lacunarity = lacunarity,
            OctaveOffsets = octaveOffsets,
            NoiseArray = noiseArray,
            ChunkStartX = chunkStartX,
            ChunkStartY = chunkStartY
        };

        var jobHandle = noiseJob.Schedule(noiseArray.Length, 64);
        jobHandle.Complete();
        octaveOffsets.Dispose();

        return noiseArray;
    }

    #endregion


    #region 基础瓦片生成

    /// <summary>
    /// 生成基础地图瓦片
    /// </summary>
    private void GenerateBaseMap(int startX, int startY, int endX, int endY, NativeArray<float> noiseArray = default)
    {
        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                // 获取瓦片位置
                Vector3Int worldPos = new(x, y, 0);



                // 缓存命中
                if (noiseArray == default)
                {
                    if (GetCustomTileFromCache(worldPos, out var baseTile))
                        _setTileCaches[0].TryAdd(worldPos, baseTile);
                }
                else
                {
                    // 计算噪声数组索引
                    int localX = x - startX;
                    int localY = y - startY;
                    int arrayIndex = localY * _chunkSize.x + localX;
                    float noiseValue = noiseArray[arrayIndex];

                    // 添加到字典
                    if (!_baseTileDict.ContainsKey(worldPos))
                    {
                        baseTileType = GetTileTypeByHeight(noiseValue);
                        _baseTileDict.Add(worldPos, baseTileType);

                        // 设置瓦片
                        if (_tileTypeToCustomTile.TryGetValue(baseTileType, out var targetTile) && targetTile != null)
                        {
                            _setTileCaches[0].TryAdd(worldPos, targetTile);
                        }
                    }
                }
            }
        }
    }


    /// <summary>
    /// 根据高度获取瓦片类型
    /// </summary>
    private TileType GetTileTypeByHeight(float height)
    {
        if (heightThresholds == null || heightThresholds.Count == 0 || terrainTiles == null || terrainTiles.Count == 0)
            return TileType.None;

        int index = heightThresholds.BinarySearch(height);
        if (index < 0)
            index = ~index;

        if (index < terrainTiles.Count)
            return terrainTiles[index].type;
        else
            return terrainTiles[terrainTiles.Count - 1].type;
    }

    #endregion


    #region 过渡瓦片生成

    /// <summary>
    /// 生成Chunk的过渡瓦片
    /// </summary>
    private void GenerateChunkTransitionTiles(int startX, int endX, int startY, int endY)
    {
        // 先修正异常瓦片
        ReplaceBoundaryTilesInRange(startX - 1, endX + 1, startY - 1, endY + 1, difCutOffNum);
        // ReplaceBoundaryTilesInRange(startX - 1, endX + 1, startY - 1, endY + 1, difCutOffNum);
        // 生成过渡瓦片
        ReplaceBoundaryTilesInRange(startX - 1, endX + 1, startY - 1, endY + 1, -1);
    }

    /// <summary>
    /// 在指定范围内替换边界瓦片
    /// </summary>
    private void ReplaceBoundaryTilesInRange(int minX, int maxX, int minY, int maxY, int cutOffNum)
    {
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector3Int currentPos = new(x, y, 0);

                if (!_baseTileDict.TryGetValue(currentPos, out TileType currentType) || currentType == TileType.None)
                    continue;

                int differentCount = 0;
                Vector3Int totalDir = Vector3Int.zero;
                tempMatchedRules.Clear();

                // 检查8个方向的邻居
                for (int dirId = 0; dirId < _dirX.Length; dirId++)
                {
                    Vector3Int neighborPos = new(x + _dirX[dirId], y + _dirY[dirId], 0);
                    if (!_baseTileDict.TryGetValue(neighborPos, out TileType neighborType))
                        continue;

                    if (neighborType == currentType)
                        continue;

                    // 查找过渡规则
                    var ruleKey = (currentType, neighborType);
                    if (_boundaryRuleDict.TryGetValue(ruleKey, out var matchedRule))
                    {
                        if (!tempMatchedRules.Exists(r =>
                            r.sourceType == matchedRule.sourceType &&
                            r.adjacentType == matchedRule.adjacentType))
                        {
                            tempMatchedRules.Add(matchedRule);
                        }
                    }

                    totalDir += new Vector3Int(_dirX[dirId], _dirY[dirId], 0);
                    differentCount++;
                }

                // 筛选最高优先级规则
                finalRule = null;
                int maxPriority = int.MinValue;
                if (tempMatchedRules.Count > 0)
                {
                    foreach (var rule in tempMatchedRules)
                    {
                        if (rule.priority > maxPriority)
                        {
                            maxPriority = rule.priority;
                            finalRule = rule;
                        }
                    }

                    // 对角方向修正
                    if (differentCount == 2)
                    {
                        if (totalDir.x == 0 && totalDir.y == 0)
                        {
                            for (int i = 4; i < 8; i++)
                            {
                                Vector3Int checkPos = new(x + _dirX[i], y + _dirY[i], 0);
                                if (_baseTileDict.TryGetValue(checkPos, out var checkType) && checkType != currentType)
                                {
                                    if (_dirX[i] == -1 && _dirY[i] == 1 || _dirX[i] == 1 && _dirY[i] == 1)
                                    {
                                        totalDir = new Vector3Int(_dirX[i], _dirY[i], 0) * 8;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    if (differentCount == 3)
                    {
                        if ((math.abs(totalDir.x) == 2 && totalDir.y == 0) ||
                            (math.abs(totalDir.y) == 2 && totalDir.x == 0))
                        {
                            Vector3Int dirCorrection = Vector3Int.zero;
                            for (int i = 0; i < 4; i++)
                            {
                                Vector3Int checkPos = new(x + _dirX[i], y + _dirY[i], 0);
                                if (_baseTileDict.TryGetValue(checkPos, out TileType checkType) && checkType != currentType)
                                {
                                    dirCorrection += new Vector3Int(_dirX[i], _dirY[i], 0);
                                }
                            }
                            totalDir = 8 * dirCorrection;
                        }
                    }
                }

                // 修正异常格子
                if (cutOffNum != -1 && finalRule != null && groundTilemap != null)
                {
                    if (differentCount >= cutOffNum || GetReplaceTile(differentCount, totalDir) == -1)
                    {
                        newType = finalRule.adjacentType;
                        _baseTileDict[currentPos] = newType;

                        if (_tileTypeToCustomTile.TryGetValue(newType, out var targetTile) && targetTile != null)
                        {
                            Vector3Int tilePos = new(x, y, 0);
                            _setTileCaches[0][tilePos] = targetTile;
                        }
                    }
                    continue;
                }

                // 生成过渡瓦片
                if (cutOffNum == -1 && finalRule != null && transitionTilemap != null)
                {
                    tileToSet = null;
                    int tileIndex = GetReplaceTile(differentCount, totalDir);

                    if (tileIndex != -1 && tileIndex < finalRule.transitionTiles.Length)
                    {
                        tileToSet = finalRule.transitionTiles[tileIndex];
                    }
                    else
                    {
                        _tileTypeToCustomTile.TryGetValue(finalRule.adjacentType, out tileToSet);
                    }

                    if (tileToSet != null)
                    {
                        Vector3Int tilePos = new(x, y, 0);
                        _tileTypeToCustomTile.TryGetValue(finalRule.adjacentType, out var TileToSetBG);
                        if (!_setTileCaches[1].ContainsKey(tilePos))
                            _setTileCaches[1].Add(tilePos, TileToSetBG);
                        if (!_setTileCaches[2].ContainsKey(tilePos))
                            _setTileCaches[2].Add(tilePos, tileToSet);
                    }
                }
            }
        }
    }

    private bool GetCustomTileFromCache(Vector3Int currentPos, out CustomTile customTile)
    {
        Vector3Int posKey = new Vector3Int(currentPos.x, currentPos.y);
        if (_baseTileDict.TryGetValue(posKey, out var tileType))
        {
            if (_tileTypeToCustomTile.TryGetValue(tileType, out var tile))
            {
                customTile = tile;
                return true;
            }
        }
        customTile = null;
        return false;
    }

    /// <summary>
    /// 获取过渡瓦片索引
    /// </summary>
    private int GetReplaceTile(int neighborCount, Vector3Int totalDir)
    {
        Vector3Int tempVector = totalDir;
        switch (neighborCount)
        {
            case 1:
                // 单个邻接时直接取方向索引
                if (_dirToIndexDict.TryGetValue(totalDir, out var dirIndex))
                    return dirIndex;
                break;

            case 2:
                // 两个邻接时按轴对齐/对角线优先级判断
                if (totalDir.x == -2) return 2;
                if (totalDir.x == 2) return 3;
                if (totalDir.y == 2) return 0;
                if (totalDir.y == -2) return 1;
                if (totalDir.x == 0 || totalDir.y == 0) return -1;
                if (_dirToIndexDict.TryGetValue(totalDir, out var diagIndex))
                    return diagIndex + 4;
                if (totalDir.x == -8 && totalDir.y == 8) return 12;
                if (totalDir.x == 8 && totalDir.y == 8) return 13;
                break;

            case 3:
                // 三个邻接时按混合方向/绝对值判断
                if ((math.abs(totalDir.x) == 1 && math.abs(totalDir.y) == 2) ||
                    (math.abs(totalDir.x) == 2 && math.abs(totalDir.y) == 1))
                {
                    tempVector = new Vector3Int(
                        totalDir.x < 0 ? -1 : 1,
                        totalDir.y < 0 ? -1 : 1,
                        0
                    );
                    if (_dirToIndexDict.TryGetValue(tempVector, out var mixIndex))
                        return mixIndex + 4;
                }
                else if (math.abs(totalDir.x) == 3)
                    return totalDir.x < 0 ? 2 : 3;
                else if (math.abs(totalDir.y) == 3)
                    return totalDir.y > 0 ? 0 : 1;
                else if (math.abs(totalDir.x) == 2 && math.abs(totalDir.y) == 2)
                {
                    tempVector = totalDir / 2;
                    if (_dirToIndexDict.TryGetValue(tempVector, out var halfIndex))
                        return halfIndex + 4;
                }
                else if (math.abs(totalDir.x) == 8 || math.abs(totalDir.y) == 8)
                {
                    tempVector = totalDir / 8;
                    if (_dirToIndexDict.TryGetValue(tempVector, out var corrIndex))
                        return corrIndex + 4;
                }
                break;

            case 4:
                // 四个邻接时取方向符号
                if (math.abs(totalDir.x) >= 2 || math.abs(totalDir.y) >= 2)
                {
                    tempVector = new Vector3Int(
                        totalDir.x < 0 ? -1 : 1,
                        totalDir.y < 0 ? -1 : 1,
                        0
                    );
                    if (_dirToIndexDict.TryGetValue(tempVector, out var fourIndex))
                        return fourIndex + 4;
                }
                break;

            case 5:
                // 五个邻接时按半值方向判断
                if (math.abs(totalDir.x) == 2 && math.abs(totalDir.y) == 2)
                {
                    tempVector = totalDir / 2;
                    if (_dirToIndexDict.TryGetValue(tempVector, out var fiveIndex))
                        return fiveIndex + 4;
                }
                break;

            // 合并空分支，保持原逻辑（6-8个邻接时无特殊处理）
            case 6:
            case 7:
            case 8:
                break;
        }
        return -1;
    }

    #endregion


    #region chunk卸载

    /// <summary>
    /// 卸载远离玩家的Chunk
    /// </summary>
    private void UnloadDistantChunks(Vector2Int centerChunkIndex)
    {
        chunksToUnload.Clear();
        foreach (var kvp in _ChunkCacheDict)
        {
            Vector2Int chunkIndex = kvp.Key;

            int distance = Mathf.Abs(chunkIndex.x - centerChunkIndex.x) + Mathf.Abs(chunkIndex.y - centerChunkIndex.y);
            if (distance > unloadDistance)
            {
                chunksToUnload.Add(chunkIndex);
            }
        }

        // 执行卸载
        foreach (var chunkIndex in chunksToUnload)
        {
            UnloadChunk(chunkIndex, onlyTile: true);
        }

        ClearLRUChunks(limit: 16);
    }

    private void ClearLRUChunks(int limit)
    {
        if (_ChunkCacheDict.Count >= limit)
        {
            var sortedChunks = _ChunkCacheDict.OrderBy(kvp => kvp.Value.lastAccessTime).Take(_ChunkCacheDict.Count - limit).ToList();
            foreach (var sortedChunk in sortedChunks)
            {
                UnloadChunk(sortedChunk.Key, onlyTile: false);
            }
        }
    }

    /// <summary>
    /// 卸载指定Chunk
    /// </summary>
    private void UnloadChunk(Vector2Int chunkIndex, bool onlyTile = true)
    {
        if (!_ChunkCacheDict.TryGetValue(chunkIndex, out var cacheData))
            return;

        // 计算Chunk的世界坐标范围
        int startX = cacheData.startX;
        int startY = cacheData.startY;
        int endX = cacheData.endX;
        int endY = cacheData.endY;

        Vector3Int[] cleanPos = new Vector3Int[(endX - startX + 1) * (endY - startY + 1)];
        Vector3Int[] expendCleanPos = new Vector3Int[(endX - startX + 3) * (endY - startY + 3)];
        TileBase[] emptyTiles = new TileBase[(endX - startX + 1) * (endY - startY + 1)];
        TileBase[] expendEmptyTiles = new TileBase[(endX - startX + 3) * (endY - startY + 3)];

        // 清除瓦片
        int i = 0;
        int j = 0;
        for (int y = startY - 1; y <= endY + 1; y++)
        {
            for (int x = startX - 1; x <= endX + 1; x++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                if (x < startX || x > endX || y < startY || y > endY)
                {
                    expendCleanPos[i++] = pos;
                }
                else
                {
                    if (!onlyTile)
                        _baseTileDict.Remove(pos);
                    expendCleanPos[i++] = pos;
                    cleanPos[j++] = pos;
                }
            }
        }

        groundTilemap.SetTiles(cleanPos, emptyTiles);
        transitionTilemap.SetTiles(expendCleanPos, expendEmptyTiles);
        transitionBackgroundTilemap.SetTiles(expendCleanPos, expendEmptyTiles);

        // 从已生成列表中移除
        _ChunkCacheDict.Remove(chunkIndex);
    }

    #endregion
}
