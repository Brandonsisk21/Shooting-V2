using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArenaShooter.Gameplay
{
    public enum MapLayout
    {
        OutdoorArena,
        TestRange,
    }

    /// <summary>
    /// Builds the chosen gray-box map, bakes bot navigation, spawns the player and bots, and starts
    /// a Free-for-All match.
    ///
    /// Runs automatically when you press Play in any scene that doesn't already contain one, so the
    /// project is playable without hand-authored scene files. F10 swaps between the arena and the
    /// test range. Replace with real scenes once the map is past gray-box.
    /// </summary>
    public class ArenaBootstrap : MonoBehaviour
    {
        public MapLayout layout = MapLayout.OutdoorArena;
        [Tooltip("Bots in the arena (GDD 3.2: 4–8 players total, including you).")]
        [Range(0, 7)] public int botCount = 5;
        public BotDifficulty botDifficulty = BotDifficulty.Normal;
        [Tooltip("Kills to win a Free-for-All match.")]
        public int scoreLimit = 25;

        public MapInfo CurrentMap { get; private set; }

        private GameObject _player;
        private bool _rebuilding;

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
            var keyboard = Keyboard.current;
            if (_rebuilding || keyboard == null || !keyboard[Key.F10].wasPressedThisFrame) return;
            layout = layout == MapLayout.OutdoorArena ? MapLayout.TestRange : MapLayout.OutdoorArena;
            StartCoroutine(Rebuild());
        }

        private void Build()
        {
            var root = new GameObject("Map_" + layout).transform;
            CurrentMap = layout == MapLayout.TestRange ? TestRangeMap.Build(root) : OutdoorArenaMap.Build(root);

            // Bake before any characters exist so only level geometry becomes walkable.
            Physics.SyncTransforms();
            var bakeArea = CurrentMap.PlayArea;
            bakeArea.Expand(4f);
            NavMeshBaker.Bake(bakeArea);

            var match = root.gameObject.AddComponent<MatchManager>();
            match.Initialize(scoreLimit);
            match.SetSpawns(CurrentMap.Spawns);
            match.SniperPad = CurrentMap.SniperPad;

            _player = BuildPlayer(CurrentMap);
            match.AddCombatant(_player.GetComponent<Combatant>());

            if (CurrentMap.HasBots)
                for (int i = 0; i < botCount; i++)
                    match.AddCombatant(BotFactory.Create(i, botDifficulty, CurrentMap.PlayArea, root));

            match.BeginMatch();
        }

        private IEnumerator Rebuild()
        {
            _rebuilding = true;
            if (CurrentMap?.Root != null) Destroy(CurrentMap.Root.gameObject);
            if (_player != null) Destroy(_player);
            foreach (var pickup in FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None))
                Destroy(pickup.gameObject);
            yield return null; // let Destroy finish so the old map isn't baked into the new nav mesh
            Build();
            _rebuilding = false;
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

            var combatant = root.AddComponent<Combatant>();
            combatant.displayName = "You";
            combatant.color = new Color(0.3f, 0.9f, 0.45f);
            combatant.isPlayer = true;
            combatant.Eyes = pivot;
            CombatantBody.Build(combatant, pivot, combatant.color, visible: false);

            var look = root.AddComponent<PlayerLook>();
            look.body = root.transform;
            look.pivot = pivot;
            look.view = cam;

            var weapons = root.AddComponent<WeaponHolder>();
            weapons.aim = pivot;
            weapons.viewModelCamera = cam;
            weapons.audioSource = audio;
            weapons.trackAimTarget = true;

            var input = root.AddComponent<PlayerInputReader>();

            var player = root.AddComponent<PlayerController>();
            player.motor = motor;
            player.look = look;
            player.weapons = weapons;
            player.health = health;
            player.input = input;

            var hud = root.AddComponent<PlayerHud>();
            hud.player = player;
            hud.combatant = combatant;
            hud.weapons = weapons;
            hud.health = health;
            hud.input = input;
            hud.view = cam;
            hud.sniperPad = map.SniperPad;
            hud.mapName = map.Name;
            return root;
        }
    }
}
