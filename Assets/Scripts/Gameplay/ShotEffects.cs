using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Cartoon shot visuals (GDD 5.1). Hits are still instant hitscan; these are cosmetic:
    /// the Pew Rifle fires short glowing bolts that fly to the hit point, the Long Zapper leaves a
    /// fading beam, and misses leave a flash + scorch mark.
    /// </summary>
    public class ShotEffects : MonoBehaviour
    {
        private enum Kind { Bolt, Beam, Flash }

        private Kind _kind;
        private LineRenderer _line;
        private Vector3 _from, _to;
        private float _lifetime, _age;
        private float _boltLength, _speed;
        private Color _color;
        private Vector3 _flashScale;

        /// <summary>Short glowing bolt traveling from muzzle to target.</summary>
        public static void PewBolt(Vector3 from, Vector3 to, Color color)
        {
            var fx = Create("PewBolt", color, 0.09f, 0.05f);
            fx._kind = Kind.Bolt;
            fx._from = from;
            fx._to = to;
            fx._speed = 220f;
            fx._boltLength = 2.2f;
            fx._lifetime = Mathf.Max(0.02f, Vector3.Distance(from, to) / fx._speed) + 0.02f;
            fx.UpdateBolt();
        }

        /// <summary>Instant zap beam that thins and fades out.</summary>
        public static void ZapBeam(Vector3 from, Vector3 to, Color color)
        {
            var fx = Create("ZapBeam", color, 0.14f, 0.08f);
            fx._kind = Kind.Beam;
            fx._lifetime = 0.35f;
            fx._line.SetPosition(0, from);
            fx._line.SetPosition(1, to);
        }

        /// <summary>A miss: quick glowing flash plus a scorch mark on the surface.</summary>
        public static void Impact(Vector3 point, Vector3 normal, Color color)
        {
            var mark = GrayBox.Visual(PrimitiveType.Cylinder, "Scorch", null, point + normal * 0.01f, new Vector3(0.22f, 0.005f, 0.22f), new Color(0.2f, 0.12f, 0.28f));
            mark.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
            Destroy(mark, 4f);

            var flash = GrayBox.GlowVisual(PrimitiveType.Sphere, "ImpactFlash", null, point + normal * 0.05f, Vector3.one * 0.1f, color);
            var fx = flash.AddComponent<ShotEffects>();
            fx._kind = Kind.Flash;
            fx._lifetime = 0.12f;
            fx._flashScale = Vector3.one * 0.45f;
        }

        private static ShotEffects Create(string name, Color color, float startWidth, float endWidth)
        {
            var go = new GameObject(name);
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.startWidth = startWidth;
            line.endWidth = endWidth;
            line.numCapVertices = 4; // rounded, blobby ends
            line.sharedMaterial = GrayBox.VertexColorUnlit;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            var fx = go.AddComponent<ShotEffects>();
            fx._line = line;
            fx._color = color;
            fx.ApplyColor(1f);
            return fx;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _lifetime)
            {
                Destroy(gameObject);
                return;
            }

            float t = _age / _lifetime;
            switch (_kind)
            {
                case Kind.Bolt:
                    UpdateBolt();
                    break;
                case Kind.Beam:
                    ApplyColor(1f - t);
                    _line.widthMultiplier = 1f - t * 0.7f;
                    break;
                case Kind.Flash:
                    transform.localScale = Vector3.Lerp(Vector3.one * 0.1f, _flashScale, t);
                    break;
            }
        }

        private void UpdateBolt()
        {
            float total = Vector3.Distance(_from, _to);
            Vector3 dir = total > 0.001f ? (_to - _from) / total : Vector3.forward;
            float head = Mathf.Min(total, _age * _speed + _boltLength);
            float tail = Mathf.Max(0f, head - _boltLength);
            _line.SetPosition(0, _from + dir * tail);
            _line.SetPosition(1, _from + dir * head);
        }

        private void ApplyColor(float alpha)
        {
            var c = new Color(_color.r, _color.g, _color.b, _color.a * alpha);
            _line.startColor = c;
            _line.endColor = Color.Lerp(c, Color.white, 0.5f);
        }
    }
}
