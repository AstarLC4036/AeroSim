using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static AeroSim.Terrain.TerrainGenerator;

namespace AeroSim.Terrain
{
    /// <summary>
    /// Height field of one chunk plus a one vertex apron around it. The apron costs
    /// <c>(res + 3)^2 - (res + 1)^2</c> extra samples and lets the vertex pass read its four
    /// neighbours instead of sampling the noise four more times per vertex.
    /// </summary>
    /// <remarks>
    /// <see cref="NoiseLayer"/> is a plain struct (enum, three values and the 4 byte PerlinNoise),
    /// so the generator can copy the inspector array straight into a NativeArray and hand it to
    /// this job without any intermediate representation.
    /// <para>FloatMode.Strict on purpose: with fast math the tail batch of a parallel loop can
    /// vectorize differently from the full batches, and the same world position could then come out
    /// with two different heights depending on which chunk asked for it. On a shared edge that is a
    /// visible crack, so the noise stays strictly reproducible.</para>
    /// </remarks>
    [BurstCompile(FloatMode = FloatMode.Strict)]
    public struct HeightFieldJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<NoiseLayer> layers;
        public float originX;
        public float originZ;
        public float spacing;
        public int side;                                  // vertices per edge (res + 1)
        [WriteOnly] public NativeArray<float> heights;    // (side + 2) squared, x fastest

        public void Execute(int index)
        {
            int apSide = side + 2;
            int row = index / apSide;
            int z = row - 1;                              // -1 .. side
            int x = index - row * apSide - 1;             // -1 .. side

            float px = originX + x * spacing;
            float pz = originZ + z * spacing;

            float height = 0f;
            for (int i = 0; i < layers.Length; i++)
            {
                height += layers[i].GetHeight(px, pz);
            }

            heights[index] = height;
        }
    }

    /// <summary>Vertices and normals of one chunk, taken from its height field.</summary>
    [BurstCompile(FloatMode = FloatMode.Strict)]
    public struct ChunkMeshJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> heights;     // (side + 2) squared, x fastest
        public float spacing;
        public int side;                                  // vertices per edge (res + 1)
        [WriteOnly] public NativeArray<Vector3> vertices; // local space, x fastest
        [WriteOnly] public NativeArray<Vector3> normals;

        public void Execute(int index)
        {
            int apSide = side + 2;
            int row = index / side;
            int z = row;
            int x = index - row * side;

            int centre = (z + 1) * apSide + x + 1;

            vertices[index] = new Vector3(x * spacing, heights[centre], z * spacing);

            // The normal of a height field is (-dh/dx, 1, -dh/dz). The 1/(2 * spacing) of the
            // central difference cancels in the normalisation, so the raw differences are used.
            float dhdx = heights[centre + 1] - heights[centre - 1];
            float dhdz = heights[centre + apSide] - heights[centre - apSide];

            float nx = -dhdx;
            float ny = 2f * spacing;
            float nz = -dhdz;
            float inv = 1f / math.sqrt(nx * nx + ny * ny + nz * nz);

            normals[index] = new Vector3(nx * inv, ny * inv, nz * inv);
        }
    }

    /// <summary>
    /// Local space bounds of a finished chunk. One thread, a fraction of a millisecond: cheap
    /// enough to avoid RecalculateBounds (and its temporaries) on the main thread.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Strict)]
    public struct ChunkBoundsJob : IJob
    {
        [ReadOnly] public NativeArray<Vector3> vertices;
        public float size;
        [WriteOnly] public NativeArray<Bounds> bounds;    // one element

        public void Execute()
        {
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < vertices.Length; i++)
            {
                float y = vertices[i].y;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            if (vertices.Length == 0)
            {
                minY = 0f;
                maxY = 0f;
            }

            // A perfectly flat chunk would otherwise get a zero sized bound.
            float halfExtentY = math.max(maxY - minY, 0.01f) * 0.5f;
            float centreY = (minY + maxY) * 0.5f;

            bounds[0] = new Bounds(
                new Vector3(size * 0.5f, centreY, size * 0.5f),
                new Vector3(size, halfExtentY * 2f, size));
        }
    }
}
