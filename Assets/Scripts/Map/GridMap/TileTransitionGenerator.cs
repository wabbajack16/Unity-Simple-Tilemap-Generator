using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

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



// /// <summary>
// /// 基于Bitmask优化的瓦片过渡生成器（完整工业级实现）
// /// </summary>
// public class TileTransitionGenerator
// {
//     // 8方向固定Bit位映射（顺序不可修改）
//     private static readonly (int dx, int dy, int bit)[] _dirBitMap = new[]
//     {
//         (0, 1, 0),   // 上 North - bit0
//         (1, 1, 1),   // 右上 Northeast - bit1
//         (1, 0, 2),   // 右 East - bit2
//         (1, -1, 3),  // 右下 Southeast - bit3
//         (0, -1, 4),  // 下 South - bit4
//         (-1, -1, 5), // 左下 Southwest - bit5
//         (-1, 0, 6),  // 左 West - bit6
//         (-1, 1, 7)   // 左上 Northwest - bit7
//     };

//     // 规则缓存：源类型 → 有序规则列表（按优先级降序）
//     private Dictionary<TileType, List<BoundaryRule>> _sortedRuleDict;

//     // Bitmask → 过渡瓦片索引 映射表（0-255）
//     private readonly int[] _bitmaskToTileIndex = new int[256];

//     // 过渡瓦片配置集（外部赋值）
//     private CustomTile[] _transitionBgTiles;  // 背景过渡瓦片
//     private CustomTile[] _transitionFgTiles;  // 前景过渡瓦片

//     /// <summary>
//     /// 初始化过渡生成器
//     /// </summary>
//     /// <param name="rules">过渡规则列表</param>
//     /// <param name="bgTiles">背景过渡瓦片集</param>
//     /// <param name="fgTiles">前景过渡瓦片集</param>
//     /// <param name="defaultBitmaskTable">预生成的Bitmask索引表</param>
//     public TileTransitionGenerator(
//         List<BoundaryRule> rules,
//         CustomTile[] bgTiles,
//         CustomTile[] fgTiles,
//         int[] defaultBitmaskTable)
//     {
//         _transitionBgTiles = bgTiles;
//         _transitionFgTiles = fgTiles;

//         // 初始化Bitmask查表
//         if (defaultBitmaskTable != null && defaultBitmaskTable.Length == 256)
//             defaultBitmaskTable.CopyTo(_bitmaskToTileIndex, 0);

//         // 预处理规则：按类型分组+优先级排序
//         PreprocessRules(rules);
//     }

//     /// <summary>
//     /// 【核心入口】生成瓦片过渡（完全兼容你原有的调用方式）
//     /// </summary>
//     /// <param name="baseTileMap">基础瓦片地图</param>
//     /// <param name="caches">3层缓存：[0]地面 [1]背景过渡 [2]前景过渡</param>
//     /// <param name="startX">起始X</param>
//     /// <param name="endX">结束X</param>
//     /// <param name="startY">起始Y</param>
//     /// <param name="endY">结束Y</param>
//     /// <param name="cutOffNum">截断阈值（-1=生成过渡瓦片，≥0=强制替换）</param>
//     public void GenerateTransitions(
//         TileType[,] baseTileMap,
//         Dictionary<Vector2Int, CustomTile>[] caches,
//         int startX, int endX, int startY, int endY,
//         int cutOffNum = -1)
//     {
//         // 缓存校验
//         if (baseTileMap == null || caches == null || caches.Length < 3) return;
//         if (_sortedRuleDict == null || _transitionBgTiles == null || _transitionFgTiles == null) return;

//         // 遍历范围扩展1格（处理边界过渡）
//         for (int y = startY - 1; y <= endY + 1; y++)
//         {
//             for (int x = startX - 1; x <= endX + 1; x++)
//             {
//                 // 1. 获取当前瓦片类型，跳过空类型
//                 TileType currentType = GetSafeTileType(baseTileMap, x, y);
//                 if (currentType == TileType.None) continue;

//                 // 2. 生成8方向邻居Bitmask（核心优化）
//                 byte neighborMask = GenerateNeighborBitmask(baseTileMap, x, y, currentType);
//                 if (neighborMask == 0) continue; // 无不同邻居，直接跳过

//                 // 3. 匹配最高优先级规则
//                 BoundaryRule finalRule = GetHighestPriorityRule(currentType, neighborMask, baseTileMap, x, y);
//                 if (finalRule == null) continue;

//                 // 4. Bitmask查表获取过渡索引
//                 int transitionIndex = _bitmaskToTileIndex[neighborMask];
//                 if (transitionIndex < 0) continue;

//                 // 5. 执行过渡逻辑（截断替换 / 生成过渡瓦片）
//                 ExecuteTransition(caches, x, y, finalRule, transitionIndex, cutOffNum, neighborMask);
//             }
//         }
//     }

//     #region 核心工具方法
//     /// <summary>
//     /// 预处理规则：分组+排序（仅初始化一次）
//     /// </summary>
//     private void PreprocessRules(List<BoundaryRule> rules)
//     {
//         _sortedRuleDict = new Dictionary<TileType, List<BoundaryRule>>();
//         if (rules == null) return;

//         foreach (var rule in rules)
//         {
//             if (!_sortedRuleDict.ContainsKey(rule.sourceType))
//                 _sortedRuleDict[rule.sourceType] = new List<BoundaryRule>();

//             _sortedRuleDict[rule.sourceType].Add(rule);
//         }

//         // 按优先级降序排序
//         foreach (var ruleList in _sortedRuleDict.Values)
//             ruleList.Sort((a, b) => b.priority.CompareTo(a.priority));
//     }

//     /// <summary>
//     /// 生成邻居Bitmask（8方向→1byte）
//     /// </summary>
//     private byte GenerateNeighborBitmask(TileType[,] map, int x, int y, TileType currentType)
//     {
//         byte mask = 0;
//         foreach (var (dx, dy, bit) in _dirBitMap)
//         {
//             TileType neighborType = GetSafeTileType(map, x + dx, y + dy);
//             if (neighborType != currentType && neighborType != TileType.None)
//                 mask |= (byte)(1 << bit);
//         }
//         return mask;
//     }

//     /// <summary>
//     /// 安全获取瓦片类型（越界返回None）
//     /// </summary>
//     private TileType GetSafeTileType(TileType[,] map, int x, int y)
//     {
//         if (x < 0 || x >= map.GetLength(0) || y < 0 || y >= map.GetLength(1))
//             return TileType.None;
//         return map[x, y];
//     }

//     /// <summary>
//     /// 获取最高优先级规则
//     /// </summary>
//     private BoundaryRule GetHighestPriorityRule(TileType sourceType, byte mask, TileType[,] map, int x, int y)
//     {
//         if (!_sortedRuleDict.TryGetValue(sourceType, out var ruleList)) return null;

//         foreach (var rule in ruleList)
//         {
//             // 校验规则匹配度
//             if (IsRuleMatch(rule, mask, map, x, y))
//                 return rule;
//         }
//         return null;
//     }

//     /// <summary>
//     /// 校验规则是否匹配
//     /// </summary>
//     private bool IsRuleMatch(BoundaryRule rule, byte mask, TileType[,] map, int x, int y)
//     {
//         foreach (var (dx, dy, bit) in _dirBitMap)
//         {
//             if ((mask & (1 << bit)) == 0) continue;
//             TileType neighborType = GetSafeTileType(map, x + dx, y + dy);
//             if (neighborType == rule.adjacentType) return true;
//         }
//         return false;
//     }

//     /// <summary>
//     /// 计算Bitmask中1的个数（不同邻居数量）
//     /// </summary>
//     private int BitCount(byte b)
//     {
//         int count = 0;
//         while (b != 0) { count++; b &= (byte)(b - 1); }
//         return count;
//     }
//     #endregion

//     #region 瓦片写入方法（你要的WriteTileToCache+WriteTransitionTile）
//     /// <summary>
//     /// 写入基础瓦片到地面层缓存
//     /// </summary>
//     private void WriteTileToCache(Dictionary<Vector2Int, CustomTile> cache, int x, int y, TileType targetType)
//     {
//         if (cache == null) return;
//         Vector2Int pos = new Vector2Int(x, y);
//         cache[pos] = new CustomTile { type = targetType };
//     }

//     /// <summary>
//     /// 写入过渡瓦片到背景+前景缓存
//     /// </summary>
//     private void WriteTransitionTile(Dictionary<Vector2Int, CustomTile>[] caches, int x, int y, int transitionIndex)
//     {
//         if (caches == null || transitionIndex < 0) return;
//         Vector2Int pos = new Vector2Int(x, y);

//         // 写入背景过渡层
//         if (transitionIndex < _transitionBgTiles.Length && _transitionBgTiles[transitionIndex] != null)
//             caches[1][pos] = _transitionBgTiles[transitionIndex];

//         // 写入前景过渡层
//         if (transitionIndex < _transitionFgTiles.Length && _transitionFgTiles[transitionIndex] != null)
//             caches[2][pos] = _transitionFgTiles[transitionIndex];
//     }

//     /// <summary>
//     /// 执行最终过渡逻辑
//     /// </summary>
//     private void ExecuteTransition(
//         Dictionary<Vector2Int, CustomTile>[] caches,
//         int x, int y,
//         BoundaryRule rule,
//         int transitionIndex,
//         int cutOffNum,
//         byte mask)
//     {
//         if (cutOffNum != -1)
//         {
//             // 截断模式：满足阈值直接替换瓦片类型
//             int differentCount = BitCount(mask);
//             if (differentCount >= cutOffNum)
//                 WriteTileToCache(caches[0], x, y, rule.adjacentType);
//         }
//         else
//         {
//             // 过渡模式：生成过渡瓦片
//             WriteTransitionTile(caches, x, y, transitionIndex);
//         }
//     }
//     #endregion
// }
