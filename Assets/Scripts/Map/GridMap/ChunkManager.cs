using UnityEngine;
using System.Collections.Generic;
using System;
using JetBrains.Annotations;
using Unity.Mathematics;
using System.Linq;
using UnityEngine.Tilemaps;
using Unity.Collections;

public class ChunkManager
{
    private NoiseGenerator noiseGenerator;  // 噪声生成
    private TerrainTileResolver tileResolver;   // 瓦片切分
    private TileTransitionGenerator transitionGenerator;    // 过渡瓦片生成
    private TilemapRender renderer; // 瓦片渲染
    private int preGenerateRadius;  // 预生成距离
    private Vector2Int[] Bottomlefttotopright;  // 地图边界
    private int unloadDistance; // 卸载距离
    private int LRULimit;

    private struct ChunkCacheData
    {
        public int startX;
        public int startY;
        public int endX;
        public int endY;
        public float lastAccessTime;
    }
    // private readonly Dictionary<Vector2Int, TileType> baseTileDict = new();
    private TileType[,] baseTileMap;
    private Dictionary<Vector2Int, ChunkCacheData> chunkCacheDict = new();
    private readonly List<Vector2Int> chunksToUnload = new();

    private Dictionary<Vector2Int, CustomTile>[] tileCaches =
    {
        new(),
        new(),
        new()
    };

    private Vector2Int chunkSize;
    private int mapMinX;
    private int mapMinY;
    private Vector2Int playerChunkIndex;

    public ChunkManager(
        NoiseGenerator noise,
        TerrainTileResolver resolver,
        TileTransitionGenerator transition,
        TilemapRender renderer,
        int preGenerateRadius,
        Vector2Int[] Bottomlefttotopright,
        int unloadDistance,
        Vector2Int chunkSize,
        int mapMinX,
        int mapMinY
        )
    {
        noiseGenerator = noise;
        tileResolver = resolver;
        transitionGenerator = transition;
        this.renderer = renderer;
        this.preGenerateRadius = preGenerateRadius;
        this.Bottomlefttotopright = Bottomlefttotopright;
        this.unloadDistance = unloadDistance;
        this.chunkSize = chunkSize;
        this.mapMinX = mapMinX;
        this.mapMinY = mapMinY;

        int width = (Bottomlefttotopright[1].x - Bottomlefttotopright[0].x) * chunkSize.x;
        int height = (Bottomlefttotopright[1].y - Bottomlefttotopright[0].y) * chunkSize.y;

        baseTileMap = new TileType[width + 2, height + 2]; // 边缘缓冲，防越界

        LRULimit =(int)(1.5f * (1 + 2 * preGenerateRadius * (preGenerateRadius + 1)));
    }

    private bool WithinLimits(Vector2Int target, Vector2Int minVector, Vector2Int maxVector)
    {
        Func<int, int, int, bool> IsInRange = (x, a, b) =>
        {
            if (a == b)
                return false;
            (a, b) = (a > b) ? (b, a) : (a, b);
            return x >= a && x <= b;
        };
        return IsInRange(target.x, minVector.x, maxVector.x) && IsInRange(target.y, minVector.y, maxVector.y);
    }

    // private bool IsInRange(int x, int a, int b)
    // {
    //     if (a == b)
    //         return false;
    //     if (a > b)
    //     {
    //        (a, b) = (b, a);
    //     }
    //     return x >= a && x <= b;
    // }

    private int ToArrayX(int x)
    {
        return x - mapMinX + 1;
    }
    private int ToArrayY(int y)
    {
        return y - mapMinY + 1;
    }

    #region Chunk生成
    public void GenerateChunkIfNotExists(Vector2Int chunkIndex)
    {
        if (chunkCacheDict.ContainsKey(chunkIndex))
        {
            var data = chunkCacheDict[chunkIndex];
            data.lastAccessTime = Time.time;
            chunkCacheDict[chunkIndex] = data;
            return;
        }

        GenerateChunk(chunkIndex);
    }
    public void GenerateChunk(Vector2Int chunkIndex)
    {
        if (chunkCacheDict.ContainsKey(chunkIndex))
        {
            var cacheData = chunkCacheDict[chunkIndex];
            cacheData.lastAccessTime = Time.time;
            chunkCacheDict[chunkIndex] = cacheData;
            return;
        }

        int startX = chunkIndex.x * chunkSize.x;
        int startY = chunkIndex.y * chunkSize.y;

        int endX = startX + chunkSize.x - 1;
        int endY = startY + chunkSize.y - 1;

        using (var noiseArray = noiseGenerator.GenerateNoise(startX, startY))
        {
            Vector2Int pos = new Vector2Int(0, 0);
            for (int y = startY; y <= endY; y++)
            {
                pos.y = y;

                for (int x = startX; x <= endX; x++)
                {
                    pos.x = x;

                    TileType type;

                    // if (!baseTileDict.TryGetValue(pos, out type))
                    type = baseTileMap[ToArrayX(x), ToArrayY(y)];
                    if (type == TileType.None)
                    {
                        int localX = x - startX;
                        int localY = y - startY;

                        int index = localY * chunkSize.x + localX;

                        float height = noiseArray[index];

                        type = tileResolver.GetTileTypeByHeight(height);

                        // baseTileDict[pos] = type;
                        baseTileMap[ToArrayX(x), ToArrayY(y)] = type;
                    }

                    tileCaches[0][pos] = tileResolver.GetTileByType(type);
                }
            }
        }

        transitionGenerator.GenerateTransitions(
            // baseTileDict,
            baseTileMap,
            tileCaches,
            startX,
            endX,
            startY,
            endY
        );

        renderer.ApplyTiles(tileCaches);

        foreach (var cache in tileCaches)
            cache.Clear();

        chunkCacheDict.Add(chunkIndex, new ChunkCacheData
        {
            startX = startX,
            startY = startY,
            endX = endX,
            endY = endY,
            lastAccessTime = Time.time
        });
    }

    /// <summary>
    /// 检查并生成玩家周围的Chunk
    /// </summary>
    public void CheckAndGenerateSurroundChunks(Vector2Int centerChunkIndex)
    {
        // 生成以玩家为中心，指定半径内的所有Chunk
        for (int y = -preGenerateRadius; y <= preGenerateRadius; y++)
        {
            for (int x = -preGenerateRadius; x <= preGenerateRadius; x++)
            {
                if (math.abs(x) + math.abs(y) <= preGenerateRadius)
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

    public void SetChunkForUpdate()
    {
        if (Player.Instance == null || Camera.main == null)
            return;

        // 获取玩家当前所在Chunk
        playerChunkIndex = GetPlayerCurrentChunkIndex();

        // 生成周围Chunk
        CheckAndGenerateSurroundChunks(playerChunkIndex);

        // 卸载过远的Chunk
        if (unloadDistance > preGenerateRadius)
            UnloadDistantChunks(playerChunkIndex);
    }

    private Vector2Int GetPlayerCurrentChunkIndex()
    {
        Vector3 playerPos = Player.Instance.GetPos();
        int chunkX = Mathf.FloorToInt(playerPos.x / chunkSize.x);
        int chunkY = Mathf.FloorToInt(playerPos.y / chunkSize.y);
        return new Vector2Int(chunkX, chunkY);
    }

    #endregion

    #region chunk卸载

    private int Chunksdistance(Vector2Int chunkAIndex, Vector2Int chunkBIndex)
    {
        return Mathf.Abs(chunkAIndex.x - chunkBIndex.x) + Mathf.Abs(chunkAIndex.y - chunkBIndex.y) + 1;
    }

    /// <summary>
    /// 卸载远离玩家的Chunk
    /// </summary>
    private void UnloadDistantChunks(Vector2Int centerChunkIndex)
    {
        chunksToUnload.Clear();
        foreach (var kvp in chunkCacheDict)
        {
            Vector2Int chunkIndex = kvp.Key;

            // int distance = Mathf.Abs(chunkIndex.x - centerChunkIndex.x) + Mathf.Abs(chunkIndex.y - centerChunkIndex.y);
            if (Chunksdistance(chunkIndex, centerChunkIndex) > unloadDistance)
            {
                chunksToUnload.Add(chunkIndex);
            }
        }

        // 执行卸载
        foreach (var chunkIndex in chunksToUnload)
        {
            UnloadChunk(chunkIndex, onlyTile: true);
        }

        ClearLRUChunks(LRULimit);
    }

    private void ClearLRUChunks(int limit)
    {
        if (chunkCacheDict.Count >= limit)
        {
            var sortedChunks = chunkCacheDict.OrderBy(kvp => kvp.Value.lastAccessTime).Take(chunkCacheDict.Count - limit).ToList();
            foreach (var sortedChunk in sortedChunks)
            {
                if(
                    Chunksdistance(sortedChunk.Key, playerChunkIndex) > unloadDistance
                )
                UnloadChunk(sortedChunk.Key, onlyTile: false);
            }
        }
    }

    /// <summary>
    /// 卸载指定Chunk
    /// </summary>
    private void UnloadChunk(Vector2Int chunkIndex, bool onlyTile = true)
    {
        if (!chunkCacheDict.TryGetValue(chunkIndex, out var cacheData))
            return;

        // 计算Chunk的世界坐标范围
        int startX = cacheData.startX;
        int startY = cacheData.startY;
        int endX = cacheData.endX;
        int endY = cacheData.endY;

        Vector2Int[] cleanPos = new Vector2Int[(endX - startX + 1) * (endY - startY + 1)];
        Vector2Int[] expendCleanPos = new Vector2Int[(endX - startX + 3) * (endY - startY + 3)];
        TileBase[] emptyTiles = new TileBase[(endX - startX + 1) * (endY - startY + 1)];
        TileBase[] expendEmptyTiles = new TileBase[(endX - startX + 3) * (endY - startY + 3)];
        Array.Fill(emptyTiles, null);
        Array.Fill(expendEmptyTiles, null);
        // 清除瓦片
        int i = 0;
        int j = 0;
        for (int y = startY - 1; y <= endY + 1; y++)
        {
            for (int x = startX - 1; x <= endX + 1; x++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (x < startX || x > endX || y < startY || y > endY)
                {
                    expendCleanPos[i++] = pos;
                }
                else
                {
                    if (!onlyTile)
                    {
                        // baseTileDict.Remove(pos);
                        baseTileMap[ToArrayX(pos.x), ToArrayY(pos.y)] = TileType.None;
                    }
                    expendCleanPos[i++] = pos;
                    cleanPos[j++] = pos;
                }
            }
        }

        renderer.SetGround(cleanPos, emptyTiles);
        renderer.SetTransition(expendCleanPos, expendEmptyTiles);
        renderer.SetBackGround(expendCleanPos, expendEmptyTiles);

        // 从已生成列表中移除
        chunkCacheDict.Remove(chunkIndex);
    }

    #endregion
}
