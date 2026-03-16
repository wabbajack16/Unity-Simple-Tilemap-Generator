using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;

public class NoiseGenerator
{
    private float noiseScale;
    private int seed;
    private int octaves;
    private float persistence;
    private float lacunarity;
    private Vector2 offset;
    private Vector2Int chunkSize;

    private NativeArray<Vector2> octaveOffsets;
    public NoiseGenerator(
        float noiseScale,
        int seed,
        int octaves,
        float persistence,
        float lacunarity,
        Vector2 offset,
        Vector2Int chunkSize)
    {
        this.noiseScale = noiseScale;
        this.seed = seed;
        this.octaves = octaves;
        this.persistence = persistence;
        this.lacunarity = lacunarity;
        this.offset = offset;
        this.chunkSize = chunkSize;

        GenerateOffset();
    }

    private void GenerateOffset()
    {
        var prng = new System.Random(seed);

        octaveOffsets = new NativeArray<Vector2>(octaves, Allocator.Persistent);

        for (int i = 0; i < octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) + offset.x;
            float offsetY = prng.Next(-100000, 100000) + offset.y;

            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        }
    }

    /// <summary>
    /// 并行生成噪声图
    /// </summary>
    public NativeArray<float> GenerateNoise(int startX, int startY)
    {
        var noiseArray = new NativeArray<float>(chunkSize.x * chunkSize.y, Allocator.TempJob);

        var noiseJob = new NoiseCalculationJob
        {
            ChunkWidth = chunkSize.x,
            ChunkHeight = chunkSize.y,
            NoiseScale = noiseScale,
            Octaves = octaves,
            Persistence = persistence,
            Lacunarity = lacunarity,
            OctaveOffsets = octaveOffsets,
            NoiseArray = noiseArray,
            ChunkStartX = startX,
            ChunkStartY = startY
        };

        var handle = noiseJob.Schedule(noiseArray.Length, 64);
        handle.Complete();

        return noiseArray;
    }

    public void Dispose()
    {
        if (octaveOffsets.IsCreated)
            octaveOffsets.Dispose();
    }
}
