using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 可复用的过渡 Sprite 槽位配置
/// 固定顺序，由 TileTransitionGenerator 的 mask 查表决定
/// </summary>
[CreateAssetMenu(fileName = "Tile Transition Set", menuName = "2D Map/Tile Transition Set")]
public class TileTransitionSet : ScriptableObject
{
    [System.Serializable]
    public class Slot
    {
        public Sprite sprite;
        public Tile.ColliderType colliderType;
    }

    [Header("过渡瓦片槽位")]
    public Slot[] transitionSlots;

    [System.NonSerialized]
    private Tile[] _runtimeTiles;

    /// <summary>
    /// 按槽位返回标准 Tile
    /// </summary>
    public Tile GetTransitionTile(int slotIndex)
    {
        if (transitionSlots == null || slotIndex < 0 || slotIndex >= transitionSlots.Length)
            return null;

        Slot slot = transitionSlots[slotIndex];
        if (slot == null || slot.sprite == null)
            return null;

        if (_runtimeTiles == null)
            _runtimeTiles = new Tile[transitionSlots.Length];

        if (_runtimeTiles[slotIndex] == null)
        {
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = slot.sprite;
            tile.colliderType = slot.colliderType;
            _runtimeTiles[slotIndex] = tile;
        }

        return _runtimeTiles[slotIndex];
    }
}
