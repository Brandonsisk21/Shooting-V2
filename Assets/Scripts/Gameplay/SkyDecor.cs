using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Alien sky dressing (GDD 5.1): a huge ringed planet, two moons and floating rocks drifting
    /// beyond the cliffs. Purely visual: no colliders, unlit so the haze doesn't wash them out.
    /// </summary>
    public static class SkyDecor
    {
        public static void Build(SymmetricBuilder b)
        {
            var sky = new GameObject("Sky").transform;
            sky.SetParent(b.Root, false);

            // Ring first and a bit farther away, so the planet draws over it (reads as a halo ring).
            var planetPos = new Vector3(-260f, 170f, 380f);
            GrayBox.GlowVisual(PrimitiveType.Cylinder, "PlanetRing", sky, planetPos * 1.02f, new Vector3(330f, 0.2f, 330f),
                new Color(1f, 0.85f, 0.7f, 0.45f)).transform.rotation = Quaternion.Euler(18f, 20f, -28f);
            GrayBox.GlowVisual(PrimitiveType.Sphere, "Planet", sky, planetPos, Vector3.one * 150f, new Color(0.98f, 0.62f, 0.5f));
            GrayBox.GlowVisual(PrimitiveType.Sphere, "MoonBig", sky, new Vector3(320f, 210f, -260f), Vector3.one * 40f, new Color(0.75f, 0.95f, 1f));
            GrayBox.GlowVisual(PrimitiveType.Sphere, "MoonSmall", sky, new Vector3(250f, 150f, -330f), Vector3.one * 16f, new Color(1f, 0.95f, 0.75f));

            // Floating rocks just outside the arena, bobbing slowly (twinned for symmetry).
            var rng = new System.Random(77);
            float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * 36f + Range(-10f, 10f);
                float distance = Range(48f, 75f);
                Vector3 pos = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
                pos.y = Range(14f, 30f);
                float size = Range(3f, 7f);
                var euler = new Vector3(Range(-20f, 20f), Range(0f, 360f), Range(-20f, 20f));
                foreach (var rock in b.Visual(PrimitiveType.Cube, "FloatingRock", pos, new Vector3(size, size * 0.6f, size * 0.8f), OutdoorPalette.RockLight, euler))
                {
                    var bob = rock.AddComponent<FloatBob>();
                    bob.amplitude = Range(0.5f, 1.2f);
                    bob.speed = Range(0.2f, 0.4f);
                    GrayBox.Visual(PrimitiveType.Cube, "Grass", rock.transform, new Vector3(0f, 0.52f, 0f), new Vector3(1.02f, 0.08f, 1.02f), OutdoorPalette.Grass);
                }
            }
        }
    }

    /// <summary>Gentle up/down drift for floating decorations.</summary>
    public class FloatBob : MonoBehaviour
    {
        public float amplitude = 1f;
        public float speed = 0.3f;

        private Vector3 _origin;
        private float _phase;

        private void Start()
        {
            _origin = transform.position;
            _phase = Random.value * Mathf.PI * 2f;
        }

        private void Update()
        {
            transform.position = _origin + Vector3.up * (Mathf.Sin(Time.time * speed * Mathf.PI * 2f + _phase) * amplitude);
        }
    }
}
