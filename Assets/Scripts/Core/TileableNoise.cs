using System;

namespace ArenaShooter.Core
{
    /// <summary>
    /// Seamless (wrapping) value noise for procedural textures: the left edge matches the right
    /// and the top matches the bottom, so textures tile without visible seams.
    /// Coordinates u, v are in [0, 1); <paramref name="period"/> is the number of lattice cells.
    /// </summary>
    public static class TileableNoise
    {
        public static float Value(float u, float v, int period, int seed)
        {
            float x = Wrap01(u) * period, y = Wrap01(v) * period;
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = x - x0, fy = y - y0;
            float sx = fx * fx * (3f - 2f * fx), sy = fy * fy * (3f - 2f * fy);
            float a = Hash(x0, y0, period, seed), b = Hash(x0 + 1, y0, period, seed);
            float c = Hash(x0, y0 + 1, period, seed), d = Hash(x0 + 1, y0 + 1, period, seed);
            return Lerp(Lerp(a, b, sx), Lerp(c, d, sx), sy);
        }

        /// <summary>Fractal (layered) noise in [0, 1]: <paramref name="octaves"/> layers, each twice as fine.</summary>
        public static float Fbm(float u, float v, int basePeriod, int octaves, int seed, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int period = basePeriod;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(u, v, period, seed + i * 101) * amp;
                norm += amp;
                amp *= gain;
                period *= 2;
            }
            return sum / norm;
        }

        /// <summary>Ridged noise (sharp creases), good for rock cracks. In [0, 1].</summary>
        public static float Ridged(float u, float v, int basePeriod, int octaves, int seed)
        {
            float n = Fbm(u, v, basePeriod, octaves, seed);
            return 1f - Math.Abs(n * 2f - 1f);
        }

        private static float Hash(int x, int y, int period, int seed)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        private static float Wrap01(float t) => t - (float)Math.Floor(t);
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
