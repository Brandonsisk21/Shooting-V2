using System;

namespace ArenaShooter.Core
{
    public enum SurfaceKind
    {
        /// <summary>Alien turf: soft blotches, fine speckle, little tufts.</summary>
        Ground,
        /// <summary>Craggy rock with cracks.</summary>
        Rock,
        /// <summary>Hull plating: panel seams, rivets, light wear.</summary>
        Plating,
    }

    /// <summary>
    /// Generates tiling surface detail for the "more realistic" graphics pass: a grayscale albedo
    /// multiplier (the material color tints it) and a tangent-space normal map from a height field.
    /// Engine-free so it can be tested and previewed; the game uploads it into textures.
    /// </summary>
    public static class SurfacePatterns
    {
        public sealed class Pattern
        {
            public int Size;
            /// <summary>Brightness multiplier per pixel (row-major), ~0.6..1.1.</summary>
            public float[] Albedo;
            /// <summary>Tangent-space normals per pixel, xyz each in [-1, 1].</summary>
            public float[] Normal;
        }

        public static Pattern Generate(SurfaceKind kind, int size, int seed = 1)
        {
            var height = new float[size * size];
            var albedo = new float[size * size];
            float bumpStrength;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                float h, a;
                switch (kind)
                {
                    case SurfaceKind.Ground:
                    {
                        float blotch = TileableNoise.Fbm(u, v, 3, 3, seed);
                        float speckle = TileableNoise.Value(u, v, 64, seed + 5);
                        float tufts = TileableNoise.Fbm(u, v, 24, 2, seed + 9);
                        h = tufts * 0.7f + speckle * 0.3f;
                        a = 0.78f + blotch * 0.26f + (speckle - 0.5f) * 0.1f;
                        break;
                    }
                    case SurfaceKind.Rock:
                    {
                        float body = TileableNoise.Fbm(u, v, 4, 5, seed);
                        float cracks = TileableNoise.Ridged(u, v, 3, 3, seed + 3);
                        float crack = Smooth(0.87f, 0.98f, cracks); // thin dark creases
                        h = body - crack * 0.35f;
                        a = 0.72f + body * 0.35f - crack * 0.25f;
                        break;
                    }
                    default: // Plating
                    {
                        // One texture = one 1 m panel; seams on the edges and a middle seam.
                        float seamU = Math.Min(Dist(u, 0f), Dist(u, 1f));
                        float seamV = Math.Min(Math.Min(Dist(v, 0f), Dist(v, 1f)), Dist(v, 0.5f));
                        float seam = 1f - Smooth(0.004f, 0.012f, Math.Min(seamU, seamV));
                        float rivet = 0f;
                        foreach (float rv in new[] { 0.04f, 0.46f, 0.54f, 0.96f })
                        foreach (float ru in new[] { 0.04f, 0.96f })
                        {
                            float d = (float)Math.Sqrt((u - ru) * (u - ru) + (v - rv) * (v - rv));
                            rivet = Math.Max(rivet, 1f - Smooth(0.006f, 0.011f, d));
                        }
                        float wear = TileableNoise.Fbm(u, v, 6, 4, seed + 11);
                        h = 0.5f - seam * 0.45f + rivet * 0.35f + (wear - 0.5f) * 0.08f;
                        a = 0.9f + (wear - 0.5f) * 0.14f - seam * 0.3f + rivet * 0.05f;
                        break;
                    }
                }
                height[y * size + x] = h;
                albedo[y * size + x] = a;
            }

            bumpStrength = kind == SurfaceKind.Plating ? 6f : kind == SurfaceKind.Rock ? 9f : 4f;
            return new Pattern { Size = size, Albedo = albedo, Normal = NormalsFromHeight(height, size, bumpStrength) };
        }

        /// <summary>Tangent-space normals from a wrapping height field (central differences).</summary>
        public static float[] NormalsFromHeight(float[] height, int size, float strength)
        {
            var normals = new float[size * size * 3];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float l = height[y * size + (x - 1 + size) % size], r = height[y * size + (x + 1) % size];
                float d = height[((y - 1 + size) % size) * size + x], u = height[((y + 1) % size) * size + x];
                float nx = (l - r) * strength, ny = (d - u) * strength, nz = 1f;
                float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                int i = (y * size + x) * 3;
                normals[i] = nx / len;
                normals[i + 1] = ny / len;
                normals[i + 2] = nz / len;
            }
            return normals;
        }

        private static float Dist(float a, float b) => Math.Abs(a - b);

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Math.Max(0f, Math.Min(1f, (x - edge0) / (edge1 - edge0)));
            return t * t * (3f - 2f * t);
        }
    }
}
