using System.Collections.Generic;
using UnityEngine.Tilemaps;

public class TerrainTileResolver
{
    private List<TerrainTile> terrainTiles;
    private List<float> thresholds;
    private readonly Dictionary<TileType, Tile> tileTypeToTile = new();

    public TerrainTileResolver(
        List<TerrainTile> terrainTiles,
        List<float> thresholds)
    {
            this.terrainTiles = terrainTiles;
        this.thresholds = thresholds;

        if (terrainTiles != null)
            foreach (var tile in terrainTiles)
            {
                tileTypeToTile[tile.type] = tile;
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

    public Tile GetTileByType(TileType type)
    {
        tileTypeToTile.TryGetValue(type, out var tile);
        return tile;
    }

    public IReadOnlyDictionary<TileType, Tile> TileTypeToTile
    {
        get { return tileTypeToTile; }
    }
}
