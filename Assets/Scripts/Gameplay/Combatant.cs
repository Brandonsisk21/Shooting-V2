using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Anyone who plays the match: the local player or a bot. Holds identity (name, color) and the
    /// shared pieces (health, weapons, motor), and keeps a registry so bots and the match can find
    /// everyone.
    /// </summary>
    public class Combatant : MonoBehaviour
    {
        private static readonly List<Combatant> Registry = new List<Combatant>();
        private static int _nextId = 1;

        public static IReadOnlyList<Combatant> All => Registry;

        public string displayName = "Player";
        public Color color = Color.white;
        public bool isPlayer;

        public int Id { get; private set; }
        // Looked up lazily so component add order doesn't matter when building at runtime.
        public Health Health => _health != null ? _health : _health = GetComponent<Health>();
        public WeaponHolder Weapons => _weapons != null ? _weapons : _weapons = GetComponent<WeaponHolder>();
        public PlayerMotor Motor => _motor != null ? _motor : _motor = GetComponent<PlayerMotor>();
        /// <summary>The look pivot (camera for the player, head for bots). Shots come from here.</summary>
        public Transform Eyes { get; set; }
        public Transform HeadCenter { get; set; }
        public Transform ChestCenter { get; set; }
        public bool IsAlive => Health != null && !Health.IsDead;

        /// <summary>Raised after the combatant is placed at a spawn and fully reset.</summary>
        public event Action<Combatant> Respawned;

        private Health _health;
        private WeaponHolder _weapons;
        private PlayerMotor _motor;
        private Renderer[] _renderers = Array.Empty<Renderer>();
        private Collider[] _hitboxes = Array.Empty<Collider>();

        private void Awake()
        {
            Id = _nextId++;
        }

        private void OnEnable() => Registry.Add(this);
        private void OnDisable() => Registry.Remove(this);

        private void Start()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            var hitboxes = GetComponentsInChildren<Hitbox>(true);
            _hitboxes = new Collider[hitboxes.Length];
            for (int i = 0; i < hitboxes.Length; i++) _hitboxes[i] = hitboxes[i].GetComponent<Collider>();
            Health.Died += (_, __) => SetPresent(false);
        }

        public void Respawn(Vector3 position, Quaternion rotation)
        {
            Health.ResetHealth();
            Weapons.ResetLoadout();
            Motor.Teleport(position, rotation);
            SetPresent(true);
            Respawned?.Invoke(this);
        }

        /// <summary>Dead combatants vanish: no body, no hitboxes, no collision.</summary>
        private void SetPresent(bool present)
        {
            foreach (var r in _renderers)
                if (r != null && !IsFirstPersonOnly(r)) r.enabled = present;
            foreach (var c in _hitboxes)
                if (c != null) c.enabled = present;
            Motor.SetCollision(present);
        }

        // The player's own view model lives under the camera and is managed by WeaponHolder.
        private bool IsFirstPersonOnly(Renderer r) => isPlayer && Eyes != null && r.transform.IsChildOf(Eyes);
    }
}
