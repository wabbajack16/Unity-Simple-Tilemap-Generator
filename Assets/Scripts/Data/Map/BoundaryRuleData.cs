using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "Boundary Rule", menuName = "2D Map/Boundary Rule")]
public class BoundaryRule : ScriptableObject
{
    // 源瓦片类型（当前瓦片）
    public TileType sourceType;

    // 相邻瓦片类型（触发边界的类型）
    public TileType adjacentType;

    // 过渡瓦片数组（按8个方向索引：0上、1右、2下、3左、4上右、5下右、6下左、7上左）
    public CustomTile[] transitionTiles;
    public bool hasBackGround;
    public int priority;
}
