using System.Collections.Generic;
using ArenaShooter.Core;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Reads keyboard/mouse input (legacy Input Manager) and drives the motor, look and weapons.
    /// Also handles the local player's death and respawn.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public PlayerMotor motor;
        public PlayerLook look;
        public WeaponHolder weapons;
        public Health health;
        public float respawnDelay = 3f;
        [Tooltip("Debug: press K to hurt yourself for this much, to test regen.")]
        public float debugSelfDamage = 25f;

        [Tooltip("Where the player can (re)spawn. If empty, respawns where the player started.")]
        public List<SpawnPoint> spawnPoints = new List<SpawnPoint>();

        public bool IsDead => health != null && health.IsDead;
        public float RespawnCountdown => IsDead ? Mathf.Max(0f, _respawnAt - Time.time) : 0f;

        private static readonly System.Random SpawnRandom = new System.Random();

        private Vector3 _fallbackPosition;
        private Quaternion _fallbackRotation;
        private float _respawnAt;

        private void Start()
        {
            _fallbackPosition = transform.position;
            _fallbackRotation = transform.rotation;
            health.Died += _ => OnDied();
            MoveToSpawn();
        }

        private void OnDied()
        {
            weapons.DropOnDeath();
            _respawnAt = Time.time + respawnDelay;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) SetCursorLocked(false);
            else if (!CursorLocked && Input.GetMouseButtonDown(0))
            {
                SetCursorLocked(true);
                return; // the click that captures the mouse shouldn't also fire
            }

            if (IsDead)
            {
                motor.Move(Vector2.zero, false, Time.deltaTime);
                if (Time.time >= _respawnAt) Respawn();
                return;
            }

            bool active = CursorLocked;
            Vector2 move = active ? ReadMove() : Vector2.zero;
            motor.Move(move, active && Input.GetKeyDown(KeyCode.Space), Time.deltaTime);

            look.Zoom = weapons.CurrentZoom;
            if (!active) return;

            look.Look(new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")));

            if (Input.GetMouseButtonDown(0)) weapons.QueueFire();
            if (Input.GetMouseButtonDown(1)) weapons.ToggleZoom();
            if (Input.GetKeyDown(KeyCode.R)) weapons.Reload();
            if (Input.GetKeyDown(KeyCode.E)) weapons.TryPickup();
            if (Input.GetKeyDown(KeyCode.Q) || Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f) weapons.SwitchNext();
            if (Input.GetKeyDown(KeyCode.Alpha1)) weapons.SwitchTo(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) weapons.SwitchTo(1);
            if (Input.GetKeyDown(KeyCode.K)) health.TakeDamage(debugSelfDamage);
        }

        private void Respawn()
        {
            health.ResetHealth();
            weapons.ResetLoadout();
            MoveToSpawn();
        }

        private void MoveToSpawn()
        {
            Vector3 position = _fallbackPosition;
            Quaternion rotation = _fallbackRotation;

            var candidates = new List<GroundPoint>(spawnPoints.Count);
            foreach (var spawn in spawnPoints)
                candidates.Add(new GroundPoint(spawn.transform.position.x, spawn.transform.position.z));
            // Enemy positions come in with bots; until then every spawn is equally safe.
            int index = SpawnSelector.Pick(candidates, null, SpawnRandom);
            if (index >= 0)
            {
                position = spawnPoints[index].transform.position;
                rotation = spawnPoints[index].transform.rotation;
            }

            motor.Teleport(position, rotation);
            look.ResetPitch();
        }

        private static Vector2 ReadMove()
        {
            float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                    - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                    - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        }

        private static bool CursorLocked => Cursor.lockState == CursorLockMode.Locked;

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
