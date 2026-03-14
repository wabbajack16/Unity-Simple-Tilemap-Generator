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

    public void ApplyTiles(
        Dictionary<Vector3Int, CustomTile>[] caches)
    {
        ground.SetTiles(
            caches[0].Keys.ToArray(),
            caches[0].Values.ToArray()
        );

        background.SetTiles(
            caches[1].Keys.ToArray(),
            caches[1].Values.ToArray()
        );

        transition.SetTiles(
            caches[2].Keys.ToArray(),
            caches[2].Values.ToArray()
        );
    }
}
