using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// The "Graphics quality" setting (Low / Medium / High): shadow quality and distance,
    /// anti-aliasing, texture filtering, post effects (bloom + filmic tone mapping) and grass density.
    /// </summary>
    public static class GraphicsSetup
    {
        public const int Low = 0, Medium = 1, High = 2;
        public static readonly string[] Names = { "Low", "Medium", "High" };

        public static int Quality { get; private set; } = High;

        /// <summary>Grass blades per square meter on the arena floor.</summary>
        public static float GrassDensity => Quality == High ? 8f : Quality == Medium ? 4f : 0f;

        public static void Apply(int quality)
        {
            Quality = Mathf.Clamp(quality, Low, High);
            QualitySettings.shadows = Quality == Low ? ShadowQuality.HardOnly : ShadowQuality.All;
            QualitySettings.shadowResolution = Quality == Low ? ShadowResolution.Medium : Quality == Medium ? ShadowResolution.High : ShadowResolution.VeryHigh;
            QualitySettings.shadowDistance = Quality == Low ? 45f : Quality == Medium ? 80f : 120f;
            QualitySettings.shadowCascades = Quality == Low ? 1 : Quality == Medium ? 2 : 4;
            QualitySettings.antiAliasing = Quality == Low ? 0 : Quality == Medium ? 2 : 4;
            QualitySettings.anisotropicFiltering = Quality == Low ? AnisotropicFiltering.Enable : AnisotropicFiltering.ForceEnable;
            QualitySettings.pixelLightCount = Quality == Low ? 1 : 4;

            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                ConfigureCamera(cam);
        }

        /// <summary>HDR rendering plus post effects on Medium and High.</summary>
        public static void ConfigureCamera(Camera cam)
        {
            if (cam == null) return;
            cam.allowHDR = true;
            cam.allowMSAA = Quality > Low;
            var fx = cam.GetComponent<PostFX>();
            bool wanted = Quality >= Medium;
            if (wanted && fx == null) fx = cam.gameObject.AddComponent<PostFX>();
            if (fx != null)
            {
                fx.enabled = wanted;
                fx.bloomIterations = Quality == High ? 6 : 4;
            }
        }
    }
}
