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

            bool sniper = Weapon.Stats.id == "sniper";
            float length = sniper ? 1.3f : 0.8f;
            Color color = sniper ? new Color(0.95f, 0.35f, 0.2f) : new Color(0.3f, 0.55f, 0.95f);

            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            _visualBase = Vector3.zero;
            GrayBox.Visual(PrimitiveType.Cube, "Body", _visual, Vector3.zero, new Vector3(0.12f, 0.16f, length), color);
            GrayBox.Visual(PrimitiveType.Cube, "Grip", _visual, new Vector3(0f, -0.14f, -length * 0.25f), new Vector3(0.08f, 0.18f, 0.1f), color * 0.7f);
            if (sniper)
            {
                var scope = GrayBox.Visual(PrimitiveType.Cylinder, "Scope", _visual, new Vector3(0f, 0.14f, 0f), new Vector3(0.08f, 0.2f, 0.08f), new Color(0.1f, 0.1f, 0.1f));
                scope.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }
    }
}
