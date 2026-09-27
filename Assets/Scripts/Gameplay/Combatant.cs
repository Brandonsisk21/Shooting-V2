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
        /// <summary>Steam ID of the human controlling this grunt online (0 for bots / offline).</summary>
        public ulong NetOwner { get; set; }
        // Looked up lazily so component add order doesn't matter when building at runtime.
        public Health Health => _health != null ? _health : _health = GetComponent<Health>();
        public WeaponHolder Weapons => _weapons != null ? _weapons : _weapons = GetComponent<WeaponHolder>();
        public PlayerMotor Motor => _motor != null ? _motor : _motor = GetComponent<PlayerMotor>();
        public GrenadeThrower Grenades => _grenades != null ? _grenades : _grenades = GetComponent<GrenadeThrower>();
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
        private GrenadeThrower _grenades;
        private Collider[] _hitboxes = Array.Empty<Collider>();

        private void Awake()
        {
            Id = _nextId++;
        }

        /// <summary>Online: use the host's ID for this combatant so everyone agrees on who is who.</summary>
        public void AssignId(int id) => Id = id;

        public static Combatant Find(int id)
        {
            foreach (var c in Registry)
                if (c.Id == id) return c;
            return null;
        }

        private void OnEnable() => Registry.Add(this);
        private void OnDisable() => Registry.Remove(this);

        private void Start()
        {
            var hitboxes = GetComponentsInChildren<Hitbox>(true);
            _hitboxes = new Collider[hitboxes.Length];
            for (int i = 0; i < hitboxes.Length; i++) _hitboxes[i] = hitboxes[i].GetComponent<Collider>();
            Health.Died += (_, __) => SetPresent(false);
        }

        public void Respawn(Vector3 position, Quaternion rotation)
        {
            Health.ResetHealth();
            Weapons.ResetLoadout();
            if (Grenades != null) Grenades.ResetPouch();
            Motor.Teleport(position, rotation);
            SetPresent(true);
            Respawned?.Invoke(this);
        }

        /// <summary>Dead combatants vanish: no body, no hitboxes, no collision.</summary>
        public void SetPresent(bool present)
        {
            // Looked up each time: held-weapon models are rebuilt on weapon changes and death.
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (!IsFirstPersonOnly(r)) r.enabled = present;
            foreach (var c in _hitboxes)
                if (c != null) c.enabled = present;
            Motor.SetCollision(present);
        }

        // The player's own view model lives under the camera and is managed by WeaponHolder.
        private bool IsFirstPersonOnly(Renderer r) => isPlayer && Eyes != null && r.transform.IsChildOf(Eyes);
    }
}
