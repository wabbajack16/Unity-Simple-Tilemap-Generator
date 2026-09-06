using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 参与高度分类的地形瓦片
/// </summary>
[CreateAssetMenu(fileName = "Terrain Tile", menuName = "2D Map/Terrain Tile")]
public class TerrainTile : Tile
{
    [Header("地形类型")]
    public TileType type;
}
