using System.Collections.Generic;
using ArenaShooter.Core;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Grenades lying on the map (GDD 2.2.1): walk over them to grab 2 (Halo-style, no button),
    /// if your pouch has room. They come back 30 s after being taken. Online, the host decides who
    /// gets them; clients only show whether each one is there.
    /// </summary>
    public class GrenadePickup : MonoBehaviour
    {
        /// <summary>All pickups on the current map, in build order (the order is the same everywhere, so it's their network ID).</summary>
        public static readonly List<GrenadePickup> All = new List<GrenadePickup>();

        public GrenadeStats stats = new GrenadeStats();
        public float grabRadius = 1.3f;

        public bool Available { get; private set; } = true;
        public int Index { get; private set; }

        private Transform _visual;
        private float _respawnAt;
        private bool _mirror;

        public static GrenadePickup Create(Vector3 position, Transform parent)
        {
            var go = new GameObject("GrenadePickup");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var pickup = go.AddComponent<GrenadePickup>();
            pickup.BuildVisual();
            return pickup;
        }

        private void OnEnable()
        {
            Index = All.Count;
            All.Add(this);
        }

        private void OnDisable() => All.Remove(this);

        public static void ResetAll()
        {
            foreach (var p in All) p.SetAvailable(true);
        }

        /// <summary>Online clients: show what the host says.</summary>
        public void SetMirror(bool available)
        {
            _mirror = true;
            if (available != Available) SetAvailable(available);
        }

        private void Update()
        {
            if (_visual != null) _visual.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
            if (_mirror) return;

            if (!Available)
            {
                if (Time.time >= _respawnAt) SetAvailable(true);
                return;
            }

            foreach (var c in Combatant.All)
            {
                if (!c.IsAlive) continue;
                Vector3 d = c.transform.position - transform.position;
                if (Mathf.Abs(d.y) > 1.5f || new Vector2(d.x, d.z).sqrMagnitude > grabRadius * grabRadius) continue;
                var thrower = c.GetComponent<GrenadeThrower>();
                if (thrower == null || thrower.Pouch.IsFull) continue;
                thrower.Pouch.Add(stats.pickupAmount);
                AudioSource.PlayClipAtPoint(ProceduralAudio.Pickup, transform.position, 0.8f);
                SetAvailable(false);
                _respawnAt = Time.time + stats.pickupRespawn;
                return;
            }
        }

        private void SetAvailable(bool available)
        {
            Available = available;
            if (_visual != null) _visual.gameObject.SetActive(available);
        }

        private void BuildVisual()
        {
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            foreach (float x in new[] { -0.13f, 0.13f })
            {
                var holder = new GameObject("Bomb").transform;
                holder.SetParent(_visual, false);
                holder.localPosition = new Vector3(x, 0.35f, 0f);
                holder.localScale = Vector3.one * 1.3f;
                if (ModelLibrary.Spawn("boom_bomb", holder, Color.white) == null)
                    GrayBox.Visual(PrimitiveType.Sphere, "Bomb", holder, Vector3.zero, Vector3.one * 0.2f, new Color(0.23f, 0.25f, 0.32f));
            }
            GrayBox.GlowVisual(PrimitiveType.Cylinder, "Pad", _visual, new Vector3(0f, 0.02f, 0f), new Vector3(0.9f, 0.01f, 0.9f), new Color(1f, 0.55f, 0.2f, 0.55f));
        }
    }
}
