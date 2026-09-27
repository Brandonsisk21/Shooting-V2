using System;
using System.Collections.Generic;
using ArenaShooter.Core;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// A thrown "boom bomb" (GDD 2.2.1): bounces around with physics, explodes after its fuse.
    /// The blast hurts everyone in range who isn't behind a wall (thrower included), pushes them
    /// back and shakes nearby cameras. Online clients show "mirror" grenades that explode when the
    /// host says so.
    /// </summary>
    public class Grenade : MonoBehaviour
    {
        public static readonly List<Grenade> Live = new List<Grenade>();
        /// <summary>A real (authoritative) grenade was thrown. The host relays it online.</summary>
        public static event Action<Grenade, Vector3> Thrown;
        /// <summary>Any grenade exploded here (cameras shake, bots react, the host relays it).</summary>
        public static event Action<Grenade, Vector3> Exploded;

        public int NetId { get; private set; }
        public Combatant Thrower { get; private set; }
        public GrenadeStats Stats { get; private set; }
        public bool IsMirror { get; private set; }
        public float TimeLeft => _explodeAt - Time.time;

        private static int _nextId = 1;
        private float _explodeAt;
        private bool _exploded;
        private Rigidbody _body;

        public static Grenade Throw(Combatant thrower, Vector3 from, Vector3 velocity, GrenadeStats stats, bool mirror = false, int netId = -1)
        {
            var go = new GameObject("Grenade");
            go.transform.position = from;
            var sphere = go.AddComponent<SphereCollider>();
            sphere.radius = 0.11f;
            var body = go.AddComponent<Rigidbody>();
            body.mass = 0.4f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.AddForce(velocity, ForceMode.VelocityChange);
            body.AddTorque(UnityEngine.Random.insideUnitSphere * 0.02f, ForceMode.Impulse);

            // Don't bounce off the thrower's own body on the way out.
            if (thrower != null)
                foreach (var c in thrower.GetComponentsInChildren<Collider>())
                    if (!c.isTrigger) Physics.IgnoreCollision(sphere, c);

            if (ModelLibrary.Spawn("boom_bomb", go.transform, Color.white) == null)
                GrayBox.Visual(PrimitiveType.Sphere, "Bomb", go.transform, Vector3.zero, Vector3.one * 0.2f, new Color(0.23f, 0.25f, 0.32f));

            var g = go.AddComponent<Grenade>();
            g.NetId = netId >= 0 ? netId : _nextId++;
            g.Thrower = thrower;
            g.Stats = stats;
            g.IsMirror = mirror;
            g._body = body;
            g._explodeAt = Time.time + stats.fuse;
            Live.Add(g);
            AudioSource.PlayClipAtPoint(ProceduralAudio.Throw, from, 0.6f);
            if (!mirror) Thrown?.Invoke(g, velocity);
            return g;
        }

        private void OnDestroy() => Live.Remove(this);

        private void Update()
        {
            if (_exploded) return;
            if (!IsMirror && Time.time >= _explodeAt) Explode(transform.position);
            else if (IsMirror && Time.time >= _explodeAt + 2f) Explode(transform.position); // host message lost: go off anyway (visual only)
        }

        /// <summary>Online client: the host's grenade <paramref name="netId"/> exploded at <paramref name="position"/>.</summary>
        public static void ExplodeMirror(int netId, Vector3 position, GrenadeStats stats)
        {
            foreach (var g in Live)
            {
                if (g.NetId != netId) continue;
                g.Explode(position);
                return;
            }
            Effects(position, stats);
            Exploded?.Invoke(null, position);
        }

        public static void ClearAll()
        {
            foreach (var g in new List<Grenade>(Live))
                if (g != null) Destroy(g.gameObject);
            Live.Clear();
        }

        private void Explode(Vector3 position)
        {
            if (_exploded) return;
            _exploded = true;
            if (!IsMirror) ApplyBlast(position);
            else ApplyLocalKnockback(position);
            Effects(position, Stats);
            Exploded?.Invoke(this, position);
            Destroy(gameObject);
        }

        /// <summary>Damage + knockback to everything in range that the blast can reach.</summary>
        private void ApplyBlast(Vector3 position)
        {
            var hit = new HashSet<Health>();
            foreach (var col in Physics.OverlapSphere(position, Stats.blastRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                var hitbox = col.GetComponent<Hitbox>();
                var health = hitbox != null ? hitbox.Owner : null;
                if (health == null || health.IsDead || !hit.Add(health)) continue;

                var combatant = health.GetComponent<Combatant>();
                Vector3 chest = combatant != null && combatant.ChestCenter != null ? combatant.ChestCenter.position : col.bounds.center;
                Vector3 head = combatant != null && combatant.HeadCenter != null ? combatant.HeadCenter.position : chest;
                if (!CanReach(position, chest, health.transform) && !CanReach(position, head, health.transform)) continue;

                float distance = Vector3.Distance(position, chest);
                float damage = Stats.DamageAt(distance);
                if (damage > 0f) health.TakeDamage(damage, new DamageSource(Thrower, "grenade", HitZone.Body));
                Knockback(health.GetComponent<PlayerMotor>(), position, distance);
            }
        }

        /// <summary>Online clients: only our own grunt gets pushed locally (the host did the damage).</summary>
        private void ApplyLocalKnockback(Vector3 position)
        {
            foreach (var c in Combatant.All)
            {
                if (!c.isPlayer || !c.IsAlive || c.ChestCenter == null) continue;
                float distance = Vector3.Distance(position, c.ChestCenter.position);
                if (distance < Stats.blastRadius && CanReach(position, c.ChestCenter.position, c.transform)) Knockback(c.Motor, position, distance);
            }
        }

        private void Knockback(PlayerMotor motor, Vector3 position, float distance)
        {
            if (motor == null || motor.GetComponent<NetProxy>() != null) return; // network-driven grunts move on their owner's machine
            Vector3 away = motor.transform.position - position;
            away.y = 0f;
            float strength = Stats.knockback * Stats.Falloff(distance);
            motor.AddImpulse((away.sqrMagnitude > 0.001f ? away.normalized : Vector3.zero) * strength + Vector3.up * strength * 0.5f);
        }

        /// <summary>Is there a clear line from the blast to this point (walls and cover block it)?</summary>
        private static bool CanReach(Vector3 from, Vector3 to, Transform target)
        {
            Vector3 start = from + Vector3.up * 0.15f;
            Vector3 delta = to - start;
            foreach (var h in Physics.RaycastAll(start, delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.isTrigger || h.collider is CharacterController) continue;
                if (h.collider.transform.IsChildOf(target)) continue;
                if (h.collider.GetComponent<Grenade>() != null) continue;
                return false;
            }
            return true;
        }

        private static void Effects(Vector3 position, GrenadeStats stats)
        {
            ShotEffects.Blast(position, stats != null ? stats.blastRadius : 5f);
            AudioSource.PlayClipAtPoint(ProceduralAudio.Boom, position, 1f);
        }
    }
}
