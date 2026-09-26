using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>Short-lived gray-box visuals: bullet tracers and impact marks.</summary>
    public class ShotEffects : MonoBehaviour
    {
        private LineRenderer _line;
        private float _lifetime;
        private float _age;
        private Color _color;

        public static void Tracer(Vector3 from, Vector3 to, Color color, float width, float lifetime = 0.08f)
        {
            var go = new GameObject("Tracer");
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = width;
            line.endWidth = width * 0.5f;
            line.sharedMaterial = GrayBox.VertexColorUnlit;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            var fx = go.AddComponent<ShotEffects>();
            fx._line = line;
            fx._lifetime = lifetime;
            fx._color = color;
            fx.ApplyColor(1f);
        }

        public static void Impact(Vector3 point, Vector3 normal)
        {
            var mark = GrayBox.Visual(PrimitiveType.Cube, "Impact", null, point + normal * 0.01f, new Vector3(0.12f, 0.12f, 0.02f), new Color(0.12f, 0.12f, 0.14f));
            mark.transform.rotation = Quaternion.LookRotation(normal);
            Destroy(mark, 4f);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _lifetime)
            {
                Destroy(gameObject);
                return;
            }
            ApplyColor(1f - _age / _lifetime);
        }

        private void ApplyColor(float alpha)
        {
            var c = new Color(_color.r, _color.g, _color.b, _color.a * alpha);
            _line.startColor = c;
            _line.endColor = c;
        }
    }
}
