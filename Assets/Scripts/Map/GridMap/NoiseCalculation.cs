using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

/// <summary>
/// 噪声计算Job
/// </summary>
[BurstCompile(CompileSynchronously = true)]
public struct NoiseCalculationJob : IJobParallelFor
{
    [ReadOnly] public int ChunkWidth;
    [ReadOnly] public int ChunkHeight;
    [ReadOnly] public float NoiseScale;
    [ReadOnly] public int Octaves;
    [ReadOnly] public float Persistence;
    [ReadOnly] public float Lacunarity;
    [ReadOnly] public NativeArray<Vector2> OctaveOffsets;
    [WriteOnly] public NativeArray<float> NoiseArray;
    [ReadOnly] public int ChunkStartX;
    [ReadOnly] public int ChunkStartY;

    public void Execute(int index)
    {
        int localX = index % ChunkWidth;
        int localY = index / ChunkWidth;
        int worldX = ChunkStartX + localX;
        int worldY = ChunkStartY + localY;

        float amplitude = 1f;
        float frequency = 1f;
        float noiseHeight = 0f;

        for (int i = 0; i < Octaves; i++)
        {
            float sampleX = (worldX + OctaveOffsets[i].x) / NoiseScale * frequency;
            float sampleY = (worldY + OctaveOffsets[i].y) / NoiseScale * frequency;
            float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2f - 1f;
            noiseHeight += perlinValue * amplitude;

            amplitude *= Persistence;
            frequency *= Lacunarity;
        }

        NoiseArray[index] = noiseHeight * 100f;
    }
}
