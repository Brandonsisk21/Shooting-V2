using UnityEngine;

namespace ArenaShooter.Gameplay
{
    public enum MapLayout
    {
        OutdoorArena,
        TestRange,
    }

    /// <summary>
    /// Builds the chosen gray-box map, the outdoor lighting and the player at runtime.
    ///
    /// Runs automatically when you press Play in any scene that doesn't already contain one, so the
    /// project is playable without hand-authored scene files. F10 swaps between the arena and the
    /// test range. Replace with real scenes once the map is past gray-box.
    /// </summary>
    public class ArenaBootstrap : MonoBehaviour
    {
        public MapLayout layout = MapLayout.OutdoorArena;

        public MapInfo CurrentMap { get; private set; }

        private GameObject _player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<ArenaBootstrap>() != null) return;
            new GameObject("ArenaBootstrap").AddComponent<ArenaBootstrap>();
        }

        private void Awake()
        {
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                Destroy(cam.gameObject);
            OutdoorEnvironment.Apply();
            Build();
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.F10)) return;
            layout = layout == MapLayout.OutdoorArena ? MapLayout.TestRange : MapLayout.OutdoorArena;
            Rebuild();
        }

        private void Build()
        {
            var root = new GameObject("Map_" + layout).transform;
            CurrentMap = layout == MapLayout.TestRange ? TestRangeMap.Build(root) : OutdoorArenaMap.Build(root);
            _player = BuildPlayer(CurrentMap);
        }

        private void Rebuild()
        {
            if (CurrentMap?.Root != null) Destroy(CurrentMap.Root.gameObject);
            if (_player != null) Destroy(_player);
            foreach (var pickup in FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None))
                Destroy(pickup.gameObject);
            Build();
        }

        private static GameObject BuildPlayer(MapInfo map)
        {
            var root = new GameObject("Player");

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
            cam.clearFlags = CameraClearFlags.Skybox;
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
            player.spawnPoints.AddRange(map.Spawns);

            var hud = root.AddComponent<PlayerHud>();
            hud.player = player;
            hud.weapons = weapons;
            hud.health = health;
            hud.view = cam;
            hud.sniperPad = map.SniperPad;
            hud.mapName = map.Name;
            return root;
        }
    }
}
