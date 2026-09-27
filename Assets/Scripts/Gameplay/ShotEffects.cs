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

        /// <summary>Grenade explosion: big flash, shockwave ring, sparks, smoke puff and a scorch mark.</summary>
        public static void Blast(Vector3 position, float radius)
        {
            var flash = GrayBox.GlowVisual(PrimitiveType.Sphere, "BlastFlash", null, position, Vector3.one * 0.3f, new Color(1f, 0.75f, 0.35f, 0.9f));
            var fx = flash.AddComponent<ShotEffects>();
            fx._kind = Kind.Flash;
            fx._lifetime = 0.22f;
            fx._flashScale = Vector3.one * radius * 0.9f;

            var ring = GrayBox.GlowVisual(PrimitiveType.Cylinder, "Shockwave", null, position + Vector3.up * 0.05f, new Vector3(0.3f, 0.01f, 0.3f), new Color(1f, 0.9f, 0.7f, 0.6f));
            var rfx = ring.AddComponent<ShotEffects>();
            rfx._kind = Kind.Flash;
            rfx._lifetime = 0.35f;
            rfx._flashScale = new Vector3(radius * 2f, 0.01f, radius * 2f);

            if (Physics.Raycast(position + Vector3.up * 0.3f, Vector3.down, out RaycastHit ground, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var scorch = GrayBox.Visual(PrimitiveType.Cylinder, "BlastScorch", null, ground.point + ground.normal * 0.01f, new Vector3(radius * 0.6f, 0.005f, radius * 0.6f), new Color(0.16f, 0.1f, 0.2f));
                scorch.transform.rotation = Quaternion.FromToRotation(Vector3.up, ground.normal);
                Destroy(scorch, 8f);
            }

            Burst(position, 45, new Color(1f, 0.85f, 0.3f), new Color(1f, 0.4f, 0.15f), 6f, 12f, 0.35f, 0.7f, 0.05f, 0.14f, 0.6f);   // sparks
            Burst(position, 18, new Color(0.75f, 0.7f, 0.85f, 0.7f), new Color(0.5f, 0.45f, 0.6f, 0.6f), 1f, 3f, 0.9f, 1.6f, 0.6f, 1.2f, -0.05f); // smoke
        }

        private static void Burst(Vector3 position, int count, Color a, Color b, float minSpeed, float maxSpeed,
            float minLife, float maxLife, float minSize, float maxSize, float gravity)
        {
            var go = new GameObject("BlastParticles");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = GrayBox.VertexColorUnlit;
            ps.Play();
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
