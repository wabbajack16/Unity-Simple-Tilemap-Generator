using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainTileResolver
{
    private List<CustomTile> terrainTiles;
    private List<float> thresholds;
    private readonly Dictionary<TileType, CustomTile> tileTypeToCustomTile = new();

    public TerrainTileResolver(
        List<CustomTile> terrainTiles,
        List<float> thresholds)
    {
            this.terrainTiles = terrainTiles;
        this.thresholds = thresholds;

        // 瓦片类型到资源的映射
        if (terrainTiles != null)
            foreach (var tile in terrainTiles)
            {
                tileTypeToCustomTile[tile.type] = tile;
            }
    }

    public TileType GetTileTypeByHeight(float height)
    {
        if (thresholds == null || thresholds.Count == 0 || terrainTiles == null || terrainTiles.Count == 0)
            return TileType.None;
        int index = thresholds.BinarySearch(height);

        if (index < 0)
            index = ~index;

        if (index < terrainTiles.Count)
            return terrainTiles[index].type;

        return terrainTiles[^1].type;
    }

    public CustomTile GetTileByType(TileType type)
    {
        tileTypeToCustomTile.TryGetValue(type, out var tile);
        return tile;
    }

    public IReadOnlyDictionary<TileType, CustomTile> TileTypeToCustomTile
    {
        get { return tileTypeToCustomTile; }
    }
}
