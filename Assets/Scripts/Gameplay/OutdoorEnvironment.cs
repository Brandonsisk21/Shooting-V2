using UnityEngine;
using UnityEngine.Rendering;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Alien-planet daytime look (GDD 5.1): lavender procedural sky, warm pink sun, soft tri-color
    /// ambient and light distance haze. Stylized, and tuned so the colorful grunts read clearly.
    /// </summary>
    public static class OutdoorEnvironment
    {
        public static readonly Color FogColor = new Color(0.8f, 0.74f, 0.94f);

        public static void Apply()
        {
            // Template from Resources so the procedural sky shader ships in builds.
            var template = Resources.Load<Material>("ArenaMaterials/Sky");
            var skyShader = template != null ? template.shader : Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var sky = template != null ? new Material(template) : new Material(skyShader);
                sky.name = "OutdoorSky";
                sky.SetFloat("_SunSize", 0.06f);
                sky.SetFloat("_AtmosphereThickness", 1.25f);
                sky.SetColor("_SkyTint", new Color(0.75f, 0.5f, 0.95f));
                sky.SetColor("_GroundColor", new Color(0.4f, 0.32f, 0.5f));
                sky.SetFloat("_Exposure", 1.3f);
                RenderSettings.skybox = sky;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.7f, 0.66f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.66f, 0.6f, 0.74f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.3f, 0.42f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 260f;

            Light sun = FindOrCreateSun();
            sun.color = new Color(1f, 0.9f, 0.86f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.78f;
            sun.shadowNormalBias = 0.3f;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = sun;

            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.85f;
            DynamicGI.UpdateEnvironment();
        }

        /// <summary>
        /// One realtime reflection probe over the whole map, rendered once after it is built, so
        /// glossy armor and metal reflect this sky and level instead of a stale default.
        /// </summary>
        public static void RefreshReflections(Transform mapRoot)
        {
            var go = new GameObject("ReflectionProbe");
            go.transform.SetParent(mapRoot, false);
            go.transform.localPosition = new Vector3(0f, 6f, 0f);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.size = new Vector3(400f, 200f, 400f);
            probe.resolution = 128;
            probe.importance = 1;
            probe.intensity = 0.9f;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.RenderProbe();
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
