using System;
using ArenaShooter.Core;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// A grunt's grenades: the pouch (start with 2, carry up to 4) and throwing. Shared by the
    /// player and bots. Online clients report their throw to the host, which spawns the real one.
    /// </summary>
    public class GrenadeThrower : MonoBehaviour
    {
        public GrenadeStats stats = new GrenadeStats();
        [Tooltip("Online client's own grunt: throws are sent to the host instead of spawned locally.")]
        public bool remoteAuthority;

        public GrenadePouch Pouch { get; private set; }
        /// <summary>Online client: we threw (from, velocity); send it to the host.</summary>
        public event Action<Vector3, Vector3> ThrowReported;

        private Combatant _owner;
        private float _readyAt;

        private void Awake()
        {
            _owner = GetComponent<Combatant>();
            Pouch = new GrenadePouch(stats.startCount, stats.maxCount);
        }

        public bool CanThrow => Pouch.Count > 0 && Time.time >= _readyAt && (_owner == null || _owner.IsAlive);

        public void ResetPouch() => Pouch.Reset(stats.startCount);

        /// <summary>Throw along an aim direction (with a little lift), from just in front of the eyes.</summary>
        public bool TryThrowAlong(Transform eyes)
        {
            Vector3 dir = (eyes.forward + Vector3.up * stats.throwLift).normalized;
            return TryThrowWithVelocity(SafeOrigin(eyes), dir * stats.throwSpeed);
        }

        /// <summary>Throw with an exact launch velocity (bots aiming a lob).</summary>
        public bool TryThrowWithVelocity(Vector3 from, Vector3 velocity)
        {
            if (!CanThrow) return false;
            Pouch.TryUse();
            _readyAt = Time.time + stats.throwCooldown;
            if (remoteAuthority) ThrowReported?.Invoke(from, velocity);
            else Grenade.Throw(_owner, from, velocity, stats);
            return true;
        }

        /// <summary>Host: throw on behalf of an online player (after checking they can).</summary>
        public bool ThrowForRemote(Vector3 from, Vector3 velocity)
        {
            if (!CanThrow) return false;
            Pouch.TryUse();
            _readyAt = Time.time + stats.throwCooldown * 0.7f; // a bit of slack for network timing
            Grenade.Throw(_owner, from, Vector3.ClampMagnitude(velocity, stats.throwSpeed * 1.2f), stats);
            return true;
        }

        /// <summary>The launch velocity for a lob that lands at <paramref name="target"/> (low arc), or a 45° throw if out of reach.</summary>
        public Vector3 LobVelocity(Vector3 from, Vector3 target)
        {
            Vector3 flat = target - from;
            float height = flat.y;
            flat.y = 0f;
            float d = flat.magnitude;
            float v = stats.throwSpeed, g = -Physics.gravity.y;
            if (d < 0.1f) return Vector3.up * v * 0.3f;
            float disc = v * v * v * v - g * (g * d * d + 2f * height * v * v);
            float angle = disc < 0f ? Mathf.PI / 4f : Mathf.Atan((v * v - Mathf.Sqrt(disc)) / (g * d));
            return flat / d * (Mathf.Cos(angle) * v) + Vector3.up * (Mathf.Sin(angle) * v);
        }

        /// <summary>Spawn point in front of the eyes, pulled back if that would be inside a wall.</summary>
        public Vector3 SafeOrigin(Transform eyes)
        {
            Vector3 wanted = eyes.position + eyes.forward * 0.6f + Vector3.down * 0.15f;
            Vector3 delta = wanted - eyes.position;
            foreach (var h in Physics.RaycastAll(eyes.position, delta.normalized, delta.magnitude + 0.15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.IsChildOf(transform) || h.collider is CharacterController) continue;
                return h.point - delta.normalized * 0.2f;
            }
            return wanted;
        }
    }
}
