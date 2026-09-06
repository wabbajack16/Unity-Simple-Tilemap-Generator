using UnityEngine;

[CreateAssetMenu(fileName = "Boundary Rule", menuName = "2D Map/Boundary Rule")]
public class BoundaryRule : ScriptableObject
{
    public TileType sourceType;

    public TileType adjacentType;

    // 引用预先配置好的过渡瓦片槽
    public TileTransitionSet transitionSet;
    public int priority;
}
