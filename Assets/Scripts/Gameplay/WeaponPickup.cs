using ArenaShooter.Core;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>A weapon lying in the world, picked up with E (GDD 2.3.1).</summary>
    public class WeaponPickup : MonoBehaviour
    {
        /// <summary>Seconds until this pickup disappears, or a negative value to keep it forever.</summary>
        public float lifetime = -1f;

        public WeaponState Weapon { get; private set; }
        /// <summary>Network ID (host-assigned). Online clients use it to ask for this pickup.</summary>
        public int NetId { get; private set; }
        /// <summary>A client-side copy of a host pickup: it never despawns on its own.</summary>
        public bool IsMirror { get; private set; }

        private static int _nextNetId = 1;
        private static readonly System.Collections.Generic.List<WeaponPickup> Registry = new System.Collections.Generic.List<WeaponPickup>();
        public static System.Collections.Generic.IReadOnlyList<WeaponPickup> All => Registry;

        public static WeaponPickup Find(int netId)
        {
            foreach (var p in Registry)
                if (p != null && p.NetId == netId) return p;
            return null;
        }

        /// <summary>Online clients: show a pickup that exists on the host.</summary>
        public static WeaponPickup CreateMirror(int netId, WeaponState weapon, Vector3 position)
        {
            var pickup = Create(weapon, position, -1f);
            pickup.NetId = netId;
            pickup.IsMirror = true;
            return pickup;
        }

        private void OnEnable() => Registry.Add(this);
        private void OnDisable() => Registry.Remove(this);

        private Transform _visual;
        private float _spawnTime;
        private Vector3 _visualBase;

        public static WeaponPickup Create(WeaponState weapon, Vector3 position, float lifetime)
        {
            var go = new GameObject("Pickup_" + weapon.Stats.id);
            go.transform.position = position;
            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.6f;

            var pickup = go.AddComponent<WeaponPickup>();
            pickup.NetId = _nextNetId++;
            pickup.lifetime = lifetime;
            pickup.SetWeapon(weapon);
            return pickup;
        }

        /// <summary>Replaces the weapon on this pickup (e.g. the sniper pad refreshing an unclaimed sniper).</summary>
        public void SetWeapon(WeaponState weapon)
        {
            Weapon = weapon;
            _spawnTime = Time.time;
            BuildVisual();
        }

        public void Consume() => Destroy(gameObject);

        private void Update()
        {
            if (lifetime >= 0f && Time.time - _spawnTime >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (_visual != null)
            {
                _visual.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
                _visual.localPosition = _visualBase + Vector3.up * (Mathf.Sin(Time.time * 2f) * 0.08f);
            }
        }

        private void BuildVisual()
        {
            if (_visual != null) Destroy(_visual.gameObject);
            if (Weapon == null) return;

            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            _visualBase = Vector3.zero;
            var model = new GameObject("Model").transform;
            model.SetParent(_visual, false);
            float length = Weapon.Stats.id == "sniper" ? 1.3f : 0.7f;
            model.localPosition = new Vector3(0f, 0f, -length * 0.8f); // center the gun on the pickup
            WeaponModels.Build(Weapon.Stats.id, model, 1.6f);
            // Soft glow disc underneath so dropped weapons are easy to spot.
            GrayBox.GlowVisual(PrimitiveType.Cylinder, "Halo", _visual, new Vector3(0f, -0.45f, 0f), new Vector3(1.2f, 0.01f, 1.2f),
                Weapon.Stats.id == "sniper" ? new Color(0.45f, 0.95f, 1f, 0.5f) : new Color(1f, 0.55f, 0.25f, 0.5f));
        }
    }
}
