using System;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace AeroSim.Terrain
{
    /// <summary>
    /// One DEM mip level, ready to be sampled. Every field is unmanaged, so a copy of this struct can
    /// be stored inside a Burst job. <see cref="NativeArray{T}"/> is itself a struct, so copying it
    /// carries a pointer and a length only - the pixels are shared, never duplicated. Handing a whole
    /// level to a chunk therefore costs nothing, and each chunk just indexes into it.
    /// </summary>
    public struct DemLevel
    {
        public NativeArray<ushort> heights;   // raw little endian, row major, x fastest, north row first
        public bool active;                   // false -> the chunk samples the noise layers instead
        public int width;
        public int height;
        public float metersPerPixel;
        public float baseMeters;              // height = baseMeters + raw * metersPerUnit
        public float metersPerUnit;
        public float originX;                 // world metres of the north-west corner (X east)
        public float originZ;                 // (Z south)

        /// <summary>
        /// Bilinear height in world metres. Shared by the chunk job and the CPU side queries, so a
        /// query and the mesh can never disagree about where the ground is.
        /// </summary>
        public float Sample(float worldX, float worldZ)
        {
            float u = (worldX - originX) / metersPerPixel;
            float v = (worldZ - originZ) / metersPerPixel;

            int x0 = (int)math.clamp(math.floor(u), 0f, width - 1);
            int y0 = (int)math.clamp(math.floor(v), 0f, height - 1);
            int x1 = math.min(x0 + 1, width - 1);
            int y1 = math.min(y0 + 1, height - 1);

            float fx = math.saturate(u - x0);
            float fy = math.saturate(v - y0);

            float a = heights[y0 * width + x0];
            float b = heights[y0 * width + x1];
            float c = heights[y1 * width + x0];
            float d = heights[y1 * width + x1];

            return baseMeters + math.lerp(math.lerp(a, b, fx), math.lerp(c, d, fx), fy) * metersPerUnit;
        }
    }

    /// <summary>
    /// Owns the native copy of one baked dataset. Loaded on the main thread, kept in
    /// <see cref="Allocator.Persistent"/> (a TempJob allocation may not outlive 4 frames) and released
    /// by <see cref="Dispose"/> - which the generator only calls after every job reading it has been
    /// completed.
    /// </summary>
    public class DemTerrainData : IDisposable
    {
        // Mirrors world.json. JsonUtility needs public fields and a [Serializable] type.
        [Serializable] public class VerticalJson { public float baseMeters; public float metersPerUnit; }
        [Serializable] public class LevelJson { public string file; public int[] pixels; public float metersPerPixel; }
        [Serializable]
        public class WorldJson
        {
            public float[] sizeMeters;
            public VerticalJson vertical;
            public LevelJson[] levels;
        }

        private DemLevel[] levels;
        private float originX, originZ, sizeX, sizeZ;

        /// <summary>Reads the dataset named by the asset. Returns null (and logs) when it is unusable.</summary>
        public static DemTerrainData Load(DemAsset asset)
        {
            string root = Path.Combine(Application.streamingAssetsPath, asset.folder);
            string descriptor = Path.Combine(root, "world.json");

            if (!File.Exists(descriptor))
            {
                Debug.LogError($"DemTerrainData: {descriptor} not found, keeping the noise layers.");
                return null;
            }

            WorldJson world = JsonUtility.FromJson<WorldJson>(File.ReadAllText(descriptor));

            if (world?.levels == null || world.levels.Length == 0 || world.vertical == null)
            {
                Debug.LogError($"DemTerrainData: {descriptor} has no levels, keeping the noise layers.");
                return null;
            }

            var data = new DemTerrainData
            {
                levels = new DemLevel[world.levels.Length],
                originX = asset.worldOrigin.x,
                originZ = asset.worldOrigin.y,
                sizeX = world.sizeMeters != null && world.sizeMeters.Length > 0 ? world.sizeMeters[0] : 0f,
                sizeZ = world.sizeMeters != null && world.sizeMeters.Length > 1 ? world.sizeMeters[1] : 0f,
            };

            for (int i = 0; i < world.levels.Length; i++)
            {
                LevelJson level = world.levels[i];
                int width = level.pixels[0];
                int height = level.pixels[1];
                int count = width * height;

                byte[] bytes = File.ReadAllBytes(Path.Combine(root, level.file));

                if (bytes.Length != count * sizeof(ushort))
                {
                    Debug.LogError($"DemTerrainData: {level.file} is {bytes.Length} bytes, expected " +
                                   $"{count * sizeof(ushort)}.");
                    data.Dispose();
                    return null;
                }

                // BlockCopy keeps this independent of the Collections version (Reinterpret<byte>(int)
                // is not available in every one of them). It costs one temporary 4 MB array, once.
                var managed = new ushort[count];
                Buffer.BlockCopy(bytes, 0, managed, 0, bytes.Length);

                var heights = new NativeArray<ushort>(count, Allocator.Persistent,
                                                     NativeArrayOptions.UninitializedMemory);
                heights.CopyFrom(managed);

                data.levels[i] = new DemLevel
                {
                    heights = heights,
                    active = true,
                    width = width,
                    height = height,
                    metersPerPixel = level.metersPerPixel,
                    baseMeters = world.vertical.baseMeters + asset.verticalOffset,
                    metersPerUnit = world.vertical.metersPerUnit,
                    originX = asset.worldOrigin.x,
                    originZ = asset.worldOrigin.y,
                };
            }

            return data;
        }

        /// <summary>
        /// Level this node should sample, or a default (uncreated) level when the node lies outside the
        /// dataset - the height job then falls back to the procedural noise path.
        /// </summary>
        public DemLevel GetLevel(TerrainNode node)
        {
            if (!Covers(node)) return default;

            // Finest level that is at least as detailed as the chunk needs. Allowing up to twice the
            // vertex spacing matters: a dataset whose resolution is only slightly coarser than the mesh
            // (33.7 m pixels against a 32 m spacing) used to be rejected, and the terrain then silently
            // fell back to the coarsest level of the pyramid.
            float spacing = node.Spacing;
            int index = 0;
            while (index + 1 < levels.Length && levels[index + 1].metersPerPixel <= spacing * 2f) index++;

            return levels[index];
        }

        /// <summary>CPU side height query, for the ground under the aircraft and similar checks.</summary>
        public float SampleHeight(float worldX, float worldZ)
        {
            return levels[0].Sample(worldX, worldZ);
        }

        public void Dispose()
        {
            if (levels == null) return;

            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i].heights.IsCreated) levels[i].heights.Dispose();
            }

            levels = null;
        }

        private bool Covers(TerrainNode node)
        {
            float x = node.offset.x, z = node.offset.y, size = node.Size;

            return x + size > originX && x < originX + sizeX &&
                   z + size > originZ && z < originZ + sizeZ;
        }
    }
}
