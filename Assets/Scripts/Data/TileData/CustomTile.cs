using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "Custom Tile", menuName = "2D Map/Custom Tile")]
public class CustomTile : Tile
{
    [Header("自定义属性")]
    public Sprite tileSprite;
    public bool hasCollider;
    public TileType type;
    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        base.GetTileData(position, tilemap, ref tileData);
        tileData.sprite = tileSprite;

        tileData.colliderType = hasCollider ? Tile.ColliderType.Sprite : Tile.ColliderType.None;
    }
}
