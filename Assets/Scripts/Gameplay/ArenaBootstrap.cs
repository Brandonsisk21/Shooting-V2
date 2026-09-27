using System;
using System.Collections;
using ArenaShooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArenaShooter.Gameplay
{
    public enum FlowState
    {
        MainMenu,
        Playing,
        Paused,
    }

    /// <summary>
    /// Entry point and game flow. On launch it shows the main menu over a live bots-only match
    /// (the camera slowly circles the arena). Starting a match rebuilds the world with the player
    /// in it; Esc / Menu pauses.
    ///
    /// Runs automatically when you press Play in any scene that doesn't already contain one, so the
    /// project is playable without hand-authored scene files.
    /// </summary>
    public class ArenaBootstrap : MonoBehaviour
    {
        [Tooltip("Bots fighting behind the main menu.")]
        [Range(0, 7)] public int menuBackgroundBots = 6;
        [Tooltip("Volume multiplier while in the main menu (the background match can get loud).")]
        [Range(0f, 1f)] public float menuVolume = 0.35f;

        public static ArenaBootstrap Instance { get; private set; }
        public static bool IsPaused => Instance != null && Instance.State == FlowState.Paused;

        public FlowState State { get; private set; } = FlowState.MainMenu;
        public MapInfo CurrentMap { get; private set; }

        private MenuUI _menu;
        private GameObject _player;
        private GameObject _menuCamera;
        private bool _switching;
        private int _stateChangedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<ArenaBootstrap>() != null) return;
            new GameObject("ArenaBootstrap").AddComponent<ArenaBootstrap>();
        }

        private void Awake()
        {
            Instance = this;
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                Destroy(cam.gameObject);
            OutdoorEnvironment.Apply();
            GameSettings.Load();
            GameSettings.Changed += ApplySettings;
            _menu = gameObject.AddComponent<MenuUI>();
            _menu.flow = this;
            ShowMainMenu();
        }

        private void OnDestroy()
        {
            GameSettings.Changed -= ApplySettings;
            Time.timeScale = 1f;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_switching || Time.frameCount == _stateChangedFrame) return;
            if (State == FlowState.Playing && MenuInput.PausePressed()) Pause();
        }

        // ------------------------------------------------------------ flow

        public void ShowMainMenu()
        {
            var background = new MatchSetup { map = MapChoice.CrashSite, botCount = menuBackgroundBots, timeLimitMinutes = 0 };
            StartCoroutine(SwitchWorld(background, withPlayer: false, () =>
            {
                State = FlowState.MainMenu;
                _menu.OpenMain();
                SetCursor(locked: false);
            }));
        }

        public void StartMatch(MatchSetup setup)
        {
            GameSettings.LastSetup = setup.Clone();
            GameSettings.Save();
            StartCoroutine(SwitchWorld(setup, withPlayer: true, () =>
            {
                State = FlowState.Playing;
                _menu.Close();
                SetCursor(locked: true);
            }));
        }

        public void Pause()
        {
            if (State != FlowState.Playing) return;
            State = FlowState.Paused;
            _stateChangedFrame = Time.frameCount;
            Time.timeScale = 0f;
            Gamepad.current?.ResetHaptics();
            _menu.OpenPause();
            SetCursor(locked: false);
        }

        public void Resume()
        {
            if (State != FlowState.Paused) return;
            State = FlowState.Playing;
            _stateChangedFrame = Time.frameCount;
            Time.timeScale = 1f;
            _menu.Close();
            SetCursor(locked: true);
        }

        private IEnumerator SwitchWorld(MatchSetup setup, bool withPlayer, Action done)
        {
            if (_switching) yield break;
            _switching = true;
            Time.timeScale = 1f;
            _menu.Close();

            if (CurrentMap?.Root != null) Destroy(CurrentMap.Root.gameObject);
            if (_player != null) Destroy(_player);
            if (_menuCamera != null) Destroy(_menuCamera);
            foreach (var pickup in FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None))
                Destroy(pickup.gameObject);
            yield return null; // let Destroy finish so the old map isn't baked into the new nav mesh

            BuildWorld(setup, withPlayer);
            if (!withPlayer) _menuCamera = MenuCamera.Create();
            ApplySettings();
            _switching = false;
            done();
        }

        private void BuildWorld(MatchSetup setup, bool withPlayer)
        {
            var root = new GameObject("Map_" + setup.map).transform;
            CurrentMap = setup.map == MapChoice.TestRange ? TestRangeMap.Build(root) : OutdoorArenaMap.Build(root);

            // Bake before any characters exist so only level geometry becomes walkable.
            Physics.SyncTransforms();
            var bakeArea = CurrentMap.PlayArea;
            bakeArea.Expand(4f);
            NavMeshBaker.Bake(bakeArea);

            var match = root.gameObject.AddComponent<MatchManager>();
            match.Initialize(setup.scoreLimit, setup.HasTimeLimit ? setup.TimeLimitSeconds : 0f);
            match.SetSpawns(CurrentMap.Spawns);
            match.SniperPad = CurrentMap.SniperPad;

            _player = null;
            if (withPlayer)
            {
                _player = BuildPlayer(CurrentMap);
                match.AddCombatant(_player.GetComponent<Combatant>());
            }

            if (CurrentMap.HasBots)
                for (int i = 0; i < setup.botCount; i++)
                    match.AddCombatant(BotFactory.Create(i, setup.difficulty, CurrentMap.PlayArea, root));

            match.BeginMatch();
        }

        private void ApplySettings()
        {
            // No player means we're behind the main menu.
            float volume = GameSettings.Volume * (_player == null ? menuVolume : 1f);
            AudioListener.volume = volume;
            if (_player != null)
                GameSettings.ApplyTo(_player.GetComponent<PlayerInputReader>(), _player.GetComponent<PlayerLook>());
        }

        private static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        // ------------------------------------------------------------ player

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
            cam.fieldOfView = GameSettings.FieldOfView;
            cam.clearFlags = CameraClearFlags.Skybox;
            pivot.gameObject.AddComponent<AudioListener>();

            var audio = root.AddComponent<AudioSource>();
            audio.spatialBlend = 0f;
            audio.playOnAwake = false;

            var motor = root.AddComponent<PlayerMotor>();
            var health = root.AddComponent<Health>();

            var combatant = root.AddComponent<Combatant>();
            combatant.displayName = "You";
            combatant.color = new Color(0.96f, 0.96f, 0.98f); // white: green/lime is taken by a bot
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

    /// <summary>Slow orbit around the arena behind the main menu.</summary>
    public class MenuCamera : MonoBehaviour
    {
        public float radius = 44f;
        public float height = 19f;
        public float degreesPerSecond = 4f;
        public Vector3 focus = new Vector3(0f, 2f, 0f);

        private float _angle = 35f;

        public static GameObject Create()
        {
            var go = new GameObject("MenuCamera");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.farClipPlane = 1000f;
            cam.clearFlags = CameraClearFlags.Skybox;
            go.AddComponent<AudioListener>();
            go.AddComponent<MenuCamera>().LateUpdate();
            return go;
        }

        private void LateUpdate()
        {
            _angle += degreesPerSecond * Time.unscaledDeltaTime;
            float rad = _angle * Mathf.Deg2Rad;
            transform.position = focus + new Vector3(Mathf.Sin(rad) * radius, height, Mathf.Cos(rad) * radius);
            transform.LookAt(focus);
        }
    }
}
