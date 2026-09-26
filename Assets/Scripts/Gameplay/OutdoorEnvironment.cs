using UnityEngine;
using UnityEngine.Rendering;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Bright daytime outdoor look (GDD 5): procedural sky, warm sun, soft tri-color ambient and
    /// light distance fog. Stylized rather than realistic, and tuned so enemies read clearly.
    /// </summary>
    public static class OutdoorEnvironment
    {
        public static readonly Color FogColor = new Color(0.74f, 0.84f, 0.95f);

        public static void Apply()
        {
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var sky = new Material(skyShader) { name = "OutdoorSky" };
                sky.SetFloat("_SunSize", 0.05f);
                sky.SetFloat("_AtmosphereThickness", 0.75f);
                sky.SetColor("_SkyTint", new Color(0.4f, 0.6f, 1f));
                sky.SetColor("_GroundColor", new Color(0.5f, 0.55f, 0.45f));
                sky.SetFloat("_Exposure", 1.3f);
                RenderSettings.skybox = sky;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.62f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.34f, 0.28f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 260f;

            Light sun = FindOrCreateSun();
            sun.color = new Color(1f, 0.95f, 0.84f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.7f;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = sun;

            DynamicGI.UpdateEnvironment();
        }

        private static Light FindOrCreateSun()
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) return light;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            return sun;
        }
    }
}
