using System.Collections.Generic;
using ArenaShooter.Core;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Procedural detail textures (albedo multiplier + normal map) for the realistic-materials pass,
    /// generated once from <see cref="SurfacePatterns"/> and cached.
    /// </summary>
    public static class SurfaceTextures
    {
        private const int Size = 256;
        private static readonly Dictionary<SurfaceKind, (Texture2D albedo, Texture2D normal)> Cache = new Dictionary<SurfaceKind, (Texture2D, Texture2D)>();

        public static (Texture2D albedo, Texture2D normal) Get(SurfaceKind kind)
        {
            if (Cache.TryGetValue(kind, out var cached) && cached.albedo != null) return cached;

            var pattern = SurfacePatterns.Generate(kind, Size, seed: 1 + (int)kind * 17);
            var albedoPixels = new Color32[Size * Size];
            var normalPixels = new Color32[Size * Size];
            for (int i = 0; i < albedoPixels.Length; i++)
            {
                byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(pattern.Albedo[i] * 232f), 0, 255);
                albedoPixels[i] = new Color32(a, a, a, 255);
                normalPixels[i] = new Color32(
                    (byte)Mathf.RoundToInt((pattern.Normal[i * 3] * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((pattern.Normal[i * 3 + 1] * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((pattern.Normal[i * 3 + 2] * 0.5f + 0.5f) * 255f),
                    255);
            }

            var albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, true, false)
            {
                name = kind + "_Albedo", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8,
            };
            albedo.SetPixels32(albedoPixels);
            albedo.Apply(true, true);

            // Linear (not sRGB): normal maps store directions, not colors.
            var normal = new Texture2D(Size, Size, TextureFormat.RGBA32, true, true)
            {
                name = kind + "_Normal", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8,
            };
            normal.SetPixels32(normalPixels);
            normal.Apply(true, true);

            Cache[kind] = (albedo, normal);
            return Cache[kind];
        }
    }
}
