using UnityEngine;
using System.Collections.Generic;

public class ChunkManager
{
    private NoiseGenerator noiseGenerator;
    private TerrainTileResolver tileResolver;
    private TileTransitionGenerator transitionGenerator;
    private TilemapRender renderer;

    private Dictionary<Vector3Int, TileType> baseTileDict = new();
    private Dictionary<Vector3Int, CustomTile>[] tileCaches =
    {
        new(),
        new(),
        new()
    };

    private Vector2Int chunkSize;

    public ChunkManager(
        NoiseGenerator noise,
        TerrainTileResolver resolver,
        TileTransitionGenerator transition,
        TilemapRender renderer,
        Vector2Int chunkSize)
    {
        noiseGenerator = noise;
        tileResolver = resolver;
        transitionGenerator = transition;
        this.renderer = renderer;
        this.chunkSize = chunkSize;
    }

    public void GenerateChunk(Vector2Int chunkIndex)
    {
        int startX = chunkIndex.x * chunkSize.x;
        int startY = chunkIndex.y * chunkSize.y;

        int endX = startX + chunkSize.x - 1;
        int endY = startY + chunkSize.y - 1;

        using var noiseArray = noiseGenerator.GenerateNoise(startX, startY);

        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                int localX = x - startX;
                int localY = y - startY;

                int index = localY * chunkSize.x + localX;

                float height = noiseArray[index];

                TileType type = tileResolver.GetTileTypeByHeight(height);

                Vector3Int pos = new(x, y, 0);

                baseTileDict[pos] = type;

                CustomTile tile = tileResolver.GetTileByType(type);

                tileCaches[0][pos] = tile;
            }
        }

        transitionGenerator.GenerateTransitions(
            baseTileDict,
            tileCaches,
            startX,
            endX,
            startY,
            endY
        );

        renderer.ApplyTiles(tileCaches);

        foreach (var cache in tileCaches)
            cache.Clear();
    }
}
