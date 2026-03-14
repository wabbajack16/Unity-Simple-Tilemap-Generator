using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class NoiseGenerator
{
    private float noiseScale;
    private int seed;
    private int octaves;
    private float persistence;
    private float lacunarity;
    private Vector2 offset;
    private Vector2Int chunkSize;
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
    }

    /// <summary>
    /// 并行生成噪声图
    /// </summary>
    public NativeArray<float> GenerateNoise(int startX, int startY)
    {
        var noiseArray = new NativeArray<float>(chunkSize.x * chunkSize.y, Allocator.TempJob);

        var prng = new System.Random(seed);

        var octaveOffsets = new NativeArray<Vector2>(octaves, Allocator.TempJob);

        for (int i = 0; i < octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) + offset.x;
            float offsetY = prng.Next(-100000, 100000) + offset.y;

            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        }

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

        octaveOffsets.Dispose();

        return noiseArray;
    }
}
