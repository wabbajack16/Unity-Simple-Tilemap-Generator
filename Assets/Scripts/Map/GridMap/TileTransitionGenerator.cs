using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TileTransitionGenerator
{
    private TerrainTileResolver resolver;

    private readonly Dictionary<(TileType, TileType), BoundaryRule> _boundaryRuleDict = new();      // 过渡瓦片集
    private int cutOffNum;

    private List<BoundaryRule> tempMatchedRules = new List<BoundaryRule>();               // 临时匹配规则
    private TileType newType;
    private CustomTile tileToSet;
    private readonly Dictionary<Vector3Int, int> _dirToIndexDict = new();        // 方向向量到索引的映射

    /// <summary>
    /// 方向数组：上、下、左、右、上右、下右、上左、下左
    /// </summary>
    private readonly int[] _dirX = { 0, 0, -1, 1, 1, 1, -1, -1 };
    private readonly int[] _dirY = { 1, -1, 0, 0, 1, -1, 1, -1 };
    private BoundaryRule finalRule;

    private int mapMinX;
    private int mapMinY;

    public TileTransitionGenerator(
        List<BoundaryRule> boundaryRules,
        TerrainTileResolver resolver,
        int cutOffNum,
        int mapMinX,
        int mapMinY
        )
    {
        // 方向向量到索引的映射
        for (int i = 0; i < _dirX.Length; i++)
        {
            var dir = new Vector3Int(_dirX[i], _dirY[i], 0);
            if (!_dirToIndexDict.ContainsKey(dir))
                _dirToIndexDict.Add(dir, i);
        }

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

        this.resolver = resolver;
        this.cutOffNum = cutOffNum;
        this.mapMinX = mapMinX;
        this.mapMinY = mapMinY;
    }

    private int ToArrayX(int x)
    {
        return x - mapMinX + 1;
    }
    private int ToArrayY(int y)
    {
        return y - mapMinY + 1;
    }


    public void GenerateTransitions(
        // Dictionary<Vector2Int, TileType> baseTiles,
        TileType[,] baseTileMap,
        Dictionary<Vector2Int, CustomTile>[] caches,
        int startX,
        int endX,
        int startY,
        int endY)
    {
        // GenerateTransition(baseTiles, caches, startX, endX, startY, endY, cutOffNum);
        // GenerateTransition(baseTiles, caches, startX, endX, startY, endY, -1);

        GenerateTransition(baseTileMap, caches, startX, endX, startY, endY, cutOffNum);
        GenerateTransition(baseTileMap, caches, startX, endX, startY, endY, -1);
    }

    private void GenerateTransition(
        // Dictionary<Vector2Int, TileType> baseTiles,
        TileType[,] baseTileMap,
        Dictionary<Vector2Int, CustomTile>[] caches,
        int startX,
        int endX,
        int startY,
        int endY,
        int cutOffNum
        )
    {
        for (int y = startY - 1; y <= endY + 1; y++)
        {
            for (int x = startX - 1; x <= endX + 1; x++)
            {
                Vector2Int currentPos = new(x, y);


                // if (!baseTiles.TryGetValue(currentPos, out TileType currentType) || currentType == TileType.None)
                //     continue;
                TileType currentType = baseTileMap[ToArrayX(currentPos.x), ToArrayY(currentPos.y)];
                if (currentType == TileType.None)
                    continue;

                int differentCount = 0;
                Vector3Int totalDir = Vector3Int.zero;
                tempMatchedRules.Clear();

                // 检查8个方向的邻居
                for (int dirId = 0; dirId < _dirX.Length; dirId++)
                {
                    Vector2Int neighborPos = new(x + _dirX[dirId], y + _dirY[dirId]);
                    // if (!baseTiles.TryGetValue(neighborPos, out TileType neighborType))
                    //     continue;
                    TileType neighborType = baseTileMap[ToArrayX(neighborPos.x), ToArrayY(neighborPos.y)];
                    if (neighborType == TileType.None)
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
                                Vector2Int checkPos = new(x + _dirX[i], y + _dirY[i]);
                                // if (baseTiles.TryGetValue(checkPos, out var checkType) && checkType != currentType)
                                TileType checkType = baseTileMap[ToArrayX(checkPos.x), ToArrayY(checkPos.y)];
                                if (checkType != TileType.None && checkType != currentType)
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
                                Vector2Int checkPos = new(x + _dirX[i], y + _dirY[i]);
                                // if (baseTiles.TryGetValue(checkPos, out TileType checkType) && checkType != currentType)
                                TileType checkType = baseTileMap[ToArrayX(checkPos.x), ToArrayY(checkPos.y)];
                                if (checkType != TileType.None && checkType != currentType)
                                {
                                    dirCorrection += new Vector3Int(_dirX[i], _dirY[i], 0);
                                }
                            }
                            totalDir = 8 * dirCorrection;
                        }
                    }
                }

                // 修正异常格子
                if (cutOffNum != -1 && finalRule != null)
                {
                    if (differentCount >= cutOffNum || GetReplaceTile(differentCount, totalDir) == -1)
                    {
                        newType = finalRule.adjacentType;
                        // baseTiles[currentPos] = newType;
                        baseTileMap[ToArrayX(currentPos.x), ToArrayY(currentPos.y)] = newType;

                        if (resolver.TileTypeToCustomTile.TryGetValue(newType, out var targetTile) && targetTile != null)
                        {
                            Vector2Int tilePos = new(x, y);
                            caches[0][tilePos] = targetTile;
                        }
                    }
                    continue;
                }

                // 生成过渡瓦片
                if (cutOffNum == -1 && finalRule != null)
                {
                    tileToSet = null;
                    int tileIndex = GetReplaceTile(differentCount, totalDir);

                    if (tileIndex != -1 && tileIndex < finalRule.transitionTiles.Length)
                    {
                        tileToSet = finalRule.transitionTiles[tileIndex];
                    }
                    else
                    {
                        resolver.TileTypeToCustomTile.TryGetValue(finalRule.adjacentType, out tileToSet);
                    }

                    if (tileToSet != null)
                    {
                        Vector2Int tilePos = new(x, y);
                        resolver.TileTypeToCustomTile.TryGetValue(finalRule.adjacentType, out var TileToSetBG);
                        if (!caches[1].ContainsKey(tilePos))
                            caches[1].Add(tilePos, TileToSetBG);
                        if (!caches[2].ContainsKey(tilePos))
                            caches[2].Add(tilePos, tileToSet);
                    }
                }
            }
        }
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
}
