using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Builds the Phase 1 gray-box test range at runtime: floor, walls, cover, a center platform
    /// with the sniper pad, target dummies at known distances, and the player.
    ///
    /// Runs automatically when you press Play in any scene that doesn't already contain one, so the
    /// project is playable without hand-authored scene files. Replace with real scenes once the
    /// Midship-style map is built.
    /// </summary>
    public class ArenaBootstrap : MonoBehaviour
    {
        public static readonly Vector3 PlayerSpawn = new Vector3(0f, 0f, -50f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<ArenaBootstrap>() != null) return;
            new GameObject("ArenaBootstrap").AddComponent<ArenaBootstrap>();
        }

        private void Awake()
        {
            RemoveSceneCameras();
            EnsureLight();
            Transform level = new GameObject("TestRange").transform;
            BuildRange(level);
            SniperSpawnPad pad = BuildCenter(level);
            BuildDummies(level);
            BuildPlayer(pad);
        }

        private static void RemoveSceneCameras()
        {
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                Destroy(cam.gameObject);
        }

        private static void EnsureLight()
        {
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) return;

            var go = new GameObject("Sun");
            var sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        /// <summary>40 x 120 m floor with 6 m walls, plus cover and jump-test blocks.</summary>
        private static void BuildRange(Transform level)
        {
            const float width = 40f, length = 120f, wallHeight = 6f;
            GrayBox.Box("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(width, 1f, length), GrayBox.Floor, level);
            GrayBox.Box("Wall_N", new Vector3(0f, wallHeight / 2, length / 2 + 0.5f), new Vector3(width + 2f, wallHeight, 1f), GrayBox.Wall, level);
            GrayBox.Box("Wall_S", new Vector3(0f, wallHeight / 2, -length / 2 - 0.5f), new Vector3(width + 2f, wallHeight, 1f), GrayBox.Wall, level);
            GrayBox.Box("Wall_E", new Vector3(width / 2 + 0.5f, wallHeight / 2, 0f), new Vector3(1f, wallHeight, length), GrayBox.Wall, level);
            GrayBox.Box("Wall_W", new Vector3(-width / 2 - 0.5f, wallHeight / 2, 0f), new Vector3(1f, wallHeight, length), GrayBox.Wall, level);

            // Jump test: 0.5 / 1.0 / 1.25 m blocks are climbable with a 1.3 m jump; 1.6 m is not.
            float[] heights = { 0.5f, 1f, 1.25f, 1.6f };
            for (int i = 0; i < heights.Length; i++)
            {
                float hgt = heights[i];
                GrayBox.Box($"JumpBlock_{hgt:0.##}m", new Vector3(10f + i * 2.5f, hgt / 2, -44f), new Vector3(2f, hgt, 2f), GrayBox.Cover, level);
            }

            // Scattered waist/chest-high cover for strafing practice.
            GrayBox.Box("Cover_A", new Vector3(6f, 0.6f, -30f), new Vector3(3f, 1.2f, 1f), GrayBox.Cover, level);
            GrayBox.Box("Cover_B", new Vector3(-6f, 0.6f, -20f), new Vector3(3f, 1.2f, 1f), GrayBox.Cover, level);
            GrayBox.Box("Cover_C", new Vector3(8f, 1f, 20f), new Vector3(1f, 2f, 4f), GrayBox.Cover, level);
            GrayBox.Box("Cover_D", new Vector3(-6f, 1f, 25f), new Vector3(1f, 2f, 4f), GrayBox.Cover, level);
        }

        /// <summary>Raised 2 m center platform with ramps front and back and the sniper pad on top.</summary>
        private static SniperSpawnPad BuildCenter(Transform level)
        {
            const float top = 2f;
            GrayBox.Box("CenterPlatform", new Vector3(0f, top / 2, 0f), new Vector3(8f, top, 8f), GrayBox.Platform, level);
            GrayBox.Ramp("Ramp_S", new Vector3(0f, 0f, -12f), new Vector3(0f, top, -4f), 3f, GrayBox.Platform, level);
            GrayBox.Ramp("Ramp_N", new Vector3(0f, 0f, 12f), new Vector3(0f, top, 4f), 3f, GrayBox.Platform, level);

            var padGo = GrayBox.Box("SniperPad", new Vector3(0f, top + 0.05f, 0f), new Vector3(1.6f, 0.1f, 1.6f), new Color(0.95f, 0.35f, 0.2f), level);
            return padGo.AddComponent<SniperSpawnPad>();
        }

        /// <summary>Dummies at known distances from the player spawn for checking damage and range.</summary>
        private static void BuildDummies(Transform level)
        {
            float[] distances = { 10f, 25f, 50f, 80f, 105f };
            foreach (float d in distances)
                TargetDummy.Create($"Dummy_{d:0}m", new Vector3(-12f, 0f, PlayerSpawn.z + d), level);

            TargetDummy.Create("Dummy_Strafing_20m", new Vector3(4f, 0f, PlayerSpawn.z + 20f), level, strafeDistance: 3f);
            TargetDummy.Create("Dummy_Strafing_40m", new Vector3(-2f, 0f, PlayerSpawn.z + 40f), level, strafeDistance: 4f);
            TargetDummy.Create("Dummy_Platform", new Vector3(3f, 2f, 2.5f), level);
        }

        private static void BuildPlayer(SniperSpawnPad pad)
        {
            var root = new GameObject("Player");
            root.transform.SetPositionAndRotation(PlayerSpawn, Quaternion.identity);

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.4f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.05f;

            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, 1.6f, 0f);
            var cam = pivot.gameObject.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 1000f;
            cam.fieldOfView = 60f;
            pivot.gameObject.AddComponent<AudioListener>();

            var audio = root.AddComponent<AudioSource>();
            audio.spatialBlend = 0f;
            audio.playOnAwake = false;

            var motor = root.AddComponent<PlayerMotor>();
            var health = root.AddComponent<Health>();

            var look = root.AddComponent<PlayerLook>();
            look.body = root.transform;
            look.pivot = pivot;
            look.view = cam;

            var weapons = root.AddComponent<WeaponHolder>();
            weapons.aimCamera = cam;
            weapons.audioSource = audio;

            var player = root.AddComponent<PlayerController>();
            player.motor = motor;
            player.look = look;
            player.weapons = weapons;
            player.health = health;

            var hud = root.AddComponent<PlayerHud>();
            hud.player = player;
            hud.weapons = weapons;
            hud.health = health;
            hud.view = cam;
            hud.sniperPad = pad;
        }
    }
}
