using UnityEngine.Tilemaps;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class TilemapRender
{
    Tilemap ground;
    Tilemap background;
    Tilemap transition;

    public TilemapRender(
        Tilemap ground,
        Tilemap background,
        Tilemap transition)
    {
        this.ground = ground;
        this.background = background;
        this.transition = transition;
    }

    private Vector3Int[] V2ToV3(Vector2Int[] vector2Ints)
    {
        Vector3Int[] vector3Ints = new Vector3Int[vector2Ints.Length];
        for (int i = 0; i < vector2Ints.Length; i++)
        {
            vector3Ints[i] = new Vector3Int(vector2Ints[i].x, vector2Ints[i].y, 0);
        }
        return vector3Ints;
    }

    public void SetGround(Vector2Int[] pos, TileBase[] tiles)
    {
        Vector3Int[] posV3 = V2ToV3(pos);
        ground.SetTiles(posV3, tiles);
    }
    public void SetBackGround(Vector2Int[] pos, TileBase[] tiles)
    {
        Vector3Int[] posV3 = V2ToV3(pos);
        background.SetTiles(posV3, tiles);
    }
    public void SetTransition(Vector2Int[] pos, TileBase[] tiles)
    {
        Vector3Int[] posV3 = V2ToV3(pos);
        transition.SetTiles(posV3, tiles);
    }
    public void ApplyTiles(
        Dictionary<Vector2Int, CustomTile>[] caches)
    {
        SetGround(
            caches[0].Keys.ToArray(),
            caches[0].Values.ToArray()
        );

        SetBackGround(
            caches[1].Keys.ToArray(),
            caches[1].Values.ToArray()
        );

        SetTransition(
            caches[2].Keys.ToArray(),
            caches[2].Values.ToArray()
        );
    }
}
