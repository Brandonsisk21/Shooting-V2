using ArenaShooter.Core;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// A 100 HP practice target with head and body hitboxes, sized like a player. Dies, then
    /// respawns in place. Can optionally strafe side to side for tracking practice.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class TargetDummy : MonoBehaviour
    {
        public float respawnDelay = 3f;
        [Tooltip("Strafe distance to each side in meters; 0 = stand still.")]
        public float strafeDistance;
        public float strafeSpeed = 6f;

        private static readonly Color BodyColor = new Color(0.95f, 0.55f, 0.15f);
        private static readonly Color HeadColor = new Color(1f, 0.85f, 0.25f);
        private static readonly Color FlashColor = new Color(1f, 0.2f, 0.2f);

        private Health _health;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private Color[] _baseColors;
        private Vector3 _origin;
        private float _flashUntil;
        private float _respawnAt = -1f;

        public static TargetDummy Create(string name, Vector3 feetPosition, Transform parent, float strafeDistance = 0f)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = feetPosition;

            // Player-sized: 1.8m tall. Body capsule 1.3m, head sphere 0.5m on top.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            body.transform.localScale = new Vector3(0.65f, 0.65f, 0.65f);
            body.AddComponent<Hitbox>().zone = HitZone.Body;

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            head.transform.localScale = Vector3.one * 0.5f;
            head.AddComponent<Hitbox>().zone = HitZone.Head;

            root.AddComponent<Health>();
            var dummy = root.AddComponent<TargetDummy>();
            dummy.strafeDistance = strafeDistance;
            return dummy;
        }

        private void Awake()
        {
            _health = GetComponent<Health>();
            _health.Damaged += (_, __) => _flashUntil = Time.time + 0.08f;
            _health.Died += _ => OnDied();
        }

        private void Start()
        {
            _origin = transform.position;
            _renderers = GetComponentsInChildren<Renderer>();
            _colliders = GetComponentsInChildren<Collider>();
            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                var hitbox = _renderers[i].GetComponent<Hitbox>();
                bool isHead = hitbox != null && hitbox.zone == HitZone.Head;
                _baseColors[i] = isHead ? HeadColor : BodyColor;
                _renderers[i].sharedMaterial = GrayBox.Mat(_baseColors[i]);
            }
        }

        private void Update()
        {
            if (_respawnAt >= 0f && Time.time >= _respawnAt) Respawn();

            if (strafeDistance > 0f && !_health.IsDead)
            {
                float offset = Mathf.PingPong(Time.time * strafeSpeed, strafeDistance * 2f) - strafeDistance;
                transform.position = _origin + transform.right * offset;
            }

            bool flashing = Time.time < _flashUntil;
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].sharedMaterial = GrayBox.Mat(flashing ? FlashColor : _baseColors[i]);
        }

        private void OnDied()
        {
            SetVisible(false);
            _respawnAt = Time.time + respawnDelay;
        }

        private void Respawn()
        {
            _respawnAt = -1f;
            _health.ResetHealth();
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            foreach (var r in _renderers) r.enabled = visible;
            foreach (var c in _colliders) c.enabled = visible;
        }
    }
}
