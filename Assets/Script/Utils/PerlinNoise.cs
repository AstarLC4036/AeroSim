using UnityEngine;

namespace AeroSim.Utils
{
    /// <summary>
    /// Seeded 2D Perlin noise (Ken Perlin's improved reference implementation).
    /// <para>No permutation table is used: the gradient of every lattice corner comes from a 32 bit
    /// integer hash of that corner and the seed. The type is therefore a plain unmanaged struct
    /// (4 bytes) that can be copied into a job struct, compiled by Burst and called from any worker
    /// thread without allocating. It also has no repetition period, which every table based
    /// implementation does have (it repeats every table size).</para>
    /// <example>
    /// <code>
    /// var noise = new PerlinNoise(20240517);
    /// float signed = noise.Sample(x, y);     // approximately -1 .. 1
    /// float unsigned = noise.Sample01(x, y); // 0 .. 1
    /// </code>
    /// </example>
    /// </summary>
    public struct PerlinNoise
    {
        /// <summary>Seed the lattice hashes are derived from.</summary>
        public int seed;

        /// <summary>Creates a sampler for <paramref name="seed"/>.</summary>
        public PerlinNoise(int seed)
        {
            this.seed = seed;
        }

        /// <summary>Read only alias of <see cref="seed"/>.</summary>
        public int Seed => seed;

        /// <summary>
        /// 2D Perlin noise at (<paramref name="x"/>, <paramref name="y"/>), approximately in [-1, 1].
        /// Integer lattice points always return exactly 0.
        /// </summary>
        public float Sample(float x, float y)
        {
            int xi = FloorToInt(x);
            int yi = FloorToInt(y);

            float xf = x - xi;
            float yf = y - yi;

            float u = Fade(xf);
            float v = Fade(yf);

            float lower = Lerp(Gradient(Hash(xi, yi), xf, yf), Gradient(Hash(xi + 1, yi), xf - 1f, yf), u);
            float upper = Lerp(Gradient(Hash(xi, yi + 1), xf, yf - 1f), Gradient(Hash(xi + 1, yi + 1), xf - 1f, yf - 1f), u);

            return Lerp(lower, upper, v);
        }

        /// <summary>Convenience overload that samples a 2D position.</summary>
        public float Sample(Vector2 position)
        {
            return Sample(position.x, position.y);
        }

        /// <summary>2D Perlin noise remapped to 0..1.</summary>
        public float Sample01(float x, float y)
        {
            return Clamp01(Sample(x, y) * 0.5f + 0.5f);
        }

        /// <summary>Convenience overload that samples a 2D position.</summary>
        public float Sample01(Vector2 position)
        {
            return Sample01(position.x, position.y);
        }

        /// <summary>Perlin's quintic smoothing curve: 6t^5 - 15t^4 + 10t^3.</summary>
        private static float Fade(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        /// <summary>Pseudo random gradient for a hash, picked from the 8 axis and diagonal directions.</summary>
        private static float Gradient(uint hash, float x, float y)
        {
            switch (hash & 7u)
            {
                case 0u: return x + y;
                case 1u: return -x + y;
                case 2u: return x - y;
                case 3u: return -x - y;
                case 4u: return x;
                case 5u: return -x;
                case 6u: return y;
                default: return -y;
            }
        }

        /// <summary>
        /// Hash of a lattice corner and the seed, finished with a full avalanche so neighbouring
        /// corners pick unrelated gradients. Pure integer math, so the result is identical on every
        /// platform and thread.
        /// </summary>
        private uint Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)x * 0x9E3779B9u + (uint)y * 0x85EBCA6Bu + (uint)seed * 0xC2B2AE35u;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }

        // Floor, lerp and clamp are written by hand instead of using Mathf, so nothing in this file
        // depends on a native Unity entry point and the whole type stays Burst friendly.
        private static int FloorToInt(float value)
        {
            int truncated = (int)value; // truncates towards zero
            return value < truncated ? truncated - 1 : truncated;
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
