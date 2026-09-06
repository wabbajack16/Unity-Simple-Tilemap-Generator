using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 基于 8 位邻接掩码的过渡瓦片生成器
/// 运行时每个格子只扫描一次 8 个邻居得到 mask，再用预生成表直接解析过渡槽位
/// </summary>
public class TileTransitionGenerator
{
    private const int DirectionCount = 8;
    private const int MaskTableSize = 1 << DirectionCount;

    // 方向顺序固定：上、下、左、右、右上、右下、左上、左下
    private static readonly int[] _dirX = { 0, 0, -1, 1, 1, 1, -1, -1 };
    private static readonly int[] _dirY = { 1, -1, 0, 0, 1, -1, 1, -1 };

    // mask 位（同上）：bit0=上, bit1=下, ..., bit7=左下
    private static readonly int[] _dirBit = { 1, 2, 4, 8, 16, 32, 64, 128 };

    private readonly Dictionary<(TileType, TileType), BoundaryRule> _boundaryRuleDict = new();

    // 邻接 mask -> 过渡数组槽位；-1 表示该组合无过渡槽，回退到基础瓦片
    private readonly int[] _maskToTileIndex = new int[MaskTableSize];
    private readonly byte[] _maskToDiffCount = new byte[MaskTableSize];

    private readonly TerrainTileResolver resolver;
    private readonly int cutOffNum;
    private readonly int mapMinX;
    private readonly int mapMinY;

    public TileTransitionGenerator(
        List<BoundaryRule> boundaryRules,
        TerrainTileResolver resolver,
        int cutOffNum,
        int mapMinX,
        int mapMinY)
    {
        if (boundaryRules != null)
        {
            foreach (var rule in boundaryRules)
            {
                var key = (rule.sourceType, rule.adjacentType);
                if (!_boundaryRuleDict.ContainsKey(key))
                    _boundaryRuleDict.Add(key, rule);
            }
        }

        BuildMaskLookupTables();

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
        TileType[,] baseTileMap,
        Dictionary<Vector2Int, TileBase>[] caches,
        int startX,
        int endX,
        int startY,
        int endY)
    {
        // 处理异常格 + 生成过渡瓦片
        GenerateTransition(baseTileMap, caches, startX, endX, startY, endY, cutOffNum);
        GenerateTransition(baseTileMap, caches, startX, endX, startY, endY, -1);
    }

    private void GenerateTransition(
        TileType[,] baseTileMap,
        Dictionary<Vector2Int, TileBase>[] caches,
        int startX,
        int endX,
        int startY,
        int endY,
        int cutOffNum)
    {
        for (int y = startY - 1; y <= endY + 1; y++)
        {
            int baseY = ToArrayY(y);

            for (int x = startX - 1; x <= endX + 1; x++)
            {
                int baseX = ToArrayX(x);
                TileType currentType = baseTileMap[baseX, baseY];
                if (currentType == TileType.None)
                    continue;

                int neighborMask = 0;
                BoundaryRule finalRule = null;
                int maxPriority = int.MinValue;

                for (int dirId = 0; dirId < DirectionCount; dirId++)
                {
                    TileType neighborType = baseTileMap[baseX + _dirX[dirId], baseY + _dirY[dirId]];
                    if (neighborType == TileType.None || neighborType == currentType)
                        continue;

                    neighborMask |= _dirBit[dirId];

                    // 按扫描顺序保留最高优先级规则，优先级相同时先匹配者优先
                    if (_boundaryRuleDict.TryGetValue((currentType, neighborType), out var matchedRule)
                        && matchedRule.priority > maxPriority)
                    {
                        finalRule = matchedRule;
                        maxPriority = matchedRule.priority;
                    }
                }

                if (finalRule == null)
                    continue;

                int tileIndex = _maskToTileIndex[neighborMask];

                // 截断模式：邻居过多或当前组合没有可用过渡槽时，把瓦片类型改成邻接类型
                if (cutOffNum != -1)
                {
                    if (_maskToDiffCount[neighborMask] >= cutOffNum || tileIndex == -1)
                    {
                        TileType newType = finalRule.adjacentType;
                        baseTileMap[baseX, baseY] = newType;

                        if (resolver.TileTypeToTile.TryGetValue(newType, out var targetTile)
                            && targetTile != null)
                        {
                            Vector2Int tilePos = new(x, y);
                            caches[0][tilePos] = targetTile;
                        }
                    }
                    continue;
                }

                // 过渡模式：写入中间层背景填充和顶层过渡瓦片
                Tile tileToSet = null;
                if (tileIndex != -1 && finalRule.transitionSet != null)
                {
                    tileToSet = finalRule.transitionSet.GetTransitionTile(tileIndex);
                }
                else
                {
                    resolver.TileTypeToTile.TryGetValue(finalRule.adjacentType, out tileToSet);
                }

                if (tileToSet == null)
                    continue;

                Vector2Int transitionPos = new(x, y);
                resolver.TileTypeToTile.TryGetValue(finalRule.adjacentType, out var backgroundTile);

                // 中间背景层瓦片缓存和顶层过渡瓦片缓存
                if (!caches[1].ContainsKey(transitionPos))
                    caches[1].Add(transitionPos, backgroundTile);
                if (!caches[2].ContainsKey(transitionPos))
                    caches[2].Add(transitionPos, tileToSet);
            }
        }
    }

    /// <summary>
    ///  邻居计数 + 方向向量解析逻辑预先计算存表
    /// </summary>
    private void BuildMaskLookupTables()
    {
        for (int mask = 0; mask < MaskTableSize; mask++)
        {
            int differentCount = 0; // 邻居数
            int sumX = 0;   // x轴方向向量和
            int sumY = 0;   // y轴方向向量和

            for (int dirId = 0; dirId < DirectionCount; dirId++)
            {
                if ((mask & _dirBit[dirId]) == 0)
                    continue;

                differentCount++;
                sumX += _dirX[dirId];
                sumY += _dirY[dirId];
            }

            int correctedX = sumX;
            int correctedY = sumY;

            // 对角修正
            if (differentCount == 2 && correctedX == 0 && correctedY == 0)
            {
                for (int dirId = 4; dirId < DirectionCount; dirId++)
                {
                    if ((mask & _dirBit[dirId]) == 0)
                        continue;

                    // 把邻居仅在<右上, 左下>/<左上, 右下>的对角方向按 8 倍向量修正做标记
                    if (dirId == 4 || dirId == 6)
                    {
                        correctedX = _dirX[dirId] * 8;
                        correctedY = _dirY[dirId] * 8;
                        break;
                    }
                }
            }
            else if (differentCount == 3
                     && ((Abs(correctedX) == 2 && correctedY == 0)
                         || (Abs(correctedY) == 2 && correctedX == 0)))
            {
                int cardinalSumX = 0;
                int cardinalSumY = 0;
                for (int dirId = 0; dirId < 4; dirId++)
                {
                    if ((mask & _dirBit[dirId]) == 0)
                        continue;
                    cardinalSumX += _dirX[dirId];
                    cardinalSumY += _dirY[dirId];
                }
                correctedX = cardinalSumX * 8;
                correctedY = cardinalSumY * 8;
            }

            _maskToDiffCount[mask] = (byte)differentCount;
            _maskToTileIndex[mask] = ResolveTileIndex(differentCount, correctedX, correctedY);
        }
    }

    private static int Abs(int value)
    {
        return value < 0 ? -value : value;
    }

    private static int FindDirIndex(int dx, int dy)
    {
        for (int dirId = 0; dirId < DirectionCount; dirId++)
        {
            if (_dirX[dirId] == dx && _dirY[dirId] == dy)
                return dirId;
        }
        return -1;
    }

    /// <summary>
    /// 计算瓦片索引
    /// </summary>
    private static int ResolveTileIndex(int differentCount, int x, int y)
    {
        int absX = Abs(x);
        int absY = Abs(y);

        switch (differentCount) // 邻居数
        {
            case 1:
                return FindDirIndex(x, y);

            case 2:
                if (x == -2) return 2;
                if (x == 2) return 3;
                if (y == 2) return 0;
                if (y == -2) return 1;
                if (x == 0 || y == 0) return -1;

                {
                    int dirId = FindDirIndex(x, y);
                    if (dirId != -1) return dirId + 4;
                }
                if (x == -8 && y == 8) return 12;
                if (x == 8 && y == 8) return 13;
                break;

            case 3:
                if ((absX == 1 && absY == 2) || (absX == 2 && absY == 1))
                {
                    int signDir = FindDirIndex(x < 0 ? -1 : 1, y < 0 ? -1 : 1);
                    if (signDir != -1) return signDir + 4;
                }
                else if (absX == 3)
                {
                    return x < 0 ? 2 : 3;
                }
                else if (absY == 3)
                {
                    return y > 0 ? 0 : 1;
                }
                else if (absX == 2 && absY == 2)
                {
                    int halfDir = FindDirIndex(x / 2, y / 2);
                    if (halfDir != -1) return halfDir + 4;
                }
                else if (absX == 8 || absY == 8)
                {
                    int scaledDir = FindDirIndex(x / 8, y / 8);
                    if (scaledDir != -1) return scaledDir + 4;
                }
                break;

            case 4:
                if (absX >= 2 || absY >= 2)
                {
                    int signDir = FindDirIndex(x < 0 ? -1 : 1, y < 0 ? -1 : 1);
                    if (signDir != -1) return signDir + 4;
                }
                break;

            case 5:
                if (absX == 2 && absY == 2)
                {
                    int halfDir = FindDirIndex(x / 2, y / 2);
                    if (halfDir != -1) return halfDir + 4;
                }
                break;
        }

        return -1;
    }
}
