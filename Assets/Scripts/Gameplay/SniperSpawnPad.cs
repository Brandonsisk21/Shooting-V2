using ArenaShooter.Core;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Center-map power weapon spawn (GDD 2.3): a sniper appears at match start and then every
    /// 90 seconds on a fixed timer, whether or not the last one was taken. An unclaimed sniper
    /// still on the pad is refreshed instead of duplicated.
    /// </summary>
    public class SniperSpawnPad : MonoBehaviour
    {
        public float interval = 90f;
        public WeaponStats weapon = WeaponStats.Sniper();
        public float hoverHeight = 1f;

        public float TimeUntilNextSpawn => _mirror ? _mirrorTimeLeft : _spawner?.TimeUntilNext ?? 0f;
        public bool HasWeaponOnPad => _mirror ? _mirrorHasWeapon : _current != null;

        private bool _mirror, _mirrorHasWeapon;
        private float _mirrorTimeLeft;

        /// <summary>Online clients: the host runs the pad; we only display its state.</summary>
        public void SetMirror(bool hasWeapon, float timeLeft)
        {
            _mirror = true;
            _mirrorHasWeapon = hasWeapon;
            _mirrorTimeLeft = timeLeft;
        }

        private FixedIntervalSpawner _spawner;
        private WeaponPickup _current;

        private void Awake()
        {
            _spawner = new FixedIntervalSpawner(interval);
        }

        /// <summary>Match restart: clear the pad and spawn a fresh sniper on the next frame.</summary>
        public void ResetTimer()
        {
            if (_current != null) _current.Consume();
            _current = null;
            _spawner = new FixedIntervalSpawner(interval);
        }

        private void Update()
        {
            if (_mirror) return;
            if (_spawner.Tick(Time.deltaTime)) Spawn();
        }

        private void Spawn()
        {
            var state = new WeaponState(weapon);
            Vector3 position = transform.position + Vector3.up * hoverHeight;
            if (_current != null) _current.SetWeapon(state);
            else _current = WeaponPickup.Create(state, position, lifetime: -1f);

            AudioSource.PlayClipAtPoint(ProceduralAudio.PowerWeaponSpawn, position, 1f);
        }
    }
}
