using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Cartoon life for a Space Grunt (GDD 5.1): a bobble-head wobble and a bouncy walk while
    /// moving, and on death the helmet pops off and they burst into confetti.
    /// </summary>
    public class GruntVisual : MonoBehaviour
    {
        public Transform visual;
        public Transform headGroup;
        public Color color = Color.white;

        private static readonly Color[] Confetti =
        {
            new Color(1f, 0.3f, 0.35f), new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.9f, 1f),
            new Color(0.5f, 1f, 0.4f), new Color(1f, 0.5f, 0.85f), Color.white,
        };

        private PlayerMotor _motor;
        private Vector3 _visualBase;
        private Quaternion _headBase;
        private float _walkPhase;

        private void Start()
        {
            _motor = GetComponent<PlayerMotor>();
            _visualBase = visual.localPosition;
            _headBase = headGroup.localRotation;
            GetComponent<Health>().Died += (_, __) => Poof();
        }

        private void Update()
        {
            Vector3 v = _motor != null ? _motor.Velocity : Vector3.zero;
            float speed01 = Mathf.Clamp01(new Vector2(v.x, v.z).magnitude / 6f);
            _walkPhase += Time.deltaTime * (4f + 10f * speed01);

            float bounce = Mathf.Abs(Mathf.Sin(_walkPhase)) * 0.05f * speed01;
            visual.localPosition = _visualBase + Vector3.up * bounce;

            // Bobble: the head wobbles more the faster they move (plus a little idle sway).
            float wobble = 2f + 7f * speed01;
            headGroup.localRotation = _headBase * Quaternion.Euler(
                Mathf.Sin(_walkPhase * 0.9f) * wobble * 0.6f,
                0f,
                Mathf.Sin(_walkPhase * 0.5f + 1f) * wobble);
        }

        private void Poof()
        {
            Vector3 chest = transform.position + Vector3.up * 0.9f;
            SpawnConfetti(chest, color);
            PopHelmet();
            AudioSource.PlayClipAtPoint(ProceduralAudio.Pop, chest, 0.8f);
        }

        private void PopHelmet()
        {
            var helmet = new GameObject("PoppedHelmet");
            helmet.layer = 2; // Ignore Raycast: shots pass through the flying helmet
            helmet.transform.SetPositionAndRotation(headGroup.position + Vector3.up * 0.25f, headGroup.rotation);
            var bubble = GrayBox.Visual(PrimitiveType.Sphere, "Bubble", helmet.transform, Vector3.zero, Vector3.one * 0.62f, Color.white);
            bubble.GetComponent<Renderer>().sharedMaterial = GrayBox.Glass(new Color(0.75f, 0.95f, 1f, 0.35f));
            GrayBox.Visual(PrimitiveType.Cylinder, "Rim", helmet.transform, new Vector3(0f, -0.25f, 0f), new Vector3(0.58f, 0.03f, 0.58f), color);

            helmet.AddComponent<SphereCollider>().radius = 0.3f;
            var body = helmet.AddComponent<Rigidbody>();
            body.mass = 0.3f;
            // An impulse rather than setting .velocity (renamed linearVelocity in Unity 6).
            body.AddForce(Vector3.up * Random.Range(5f, 7f) + Random.insideUnitSphere * 2f, ForceMode.VelocityChange);
            body.angularVelocity = Random.insideUnitSphere * 10f;
            Destroy(helmet, 4f);
        }

        public static void SpawnConfetti(Vector3 position, Color accent)
        {
            var go = new GameObject("Confetti");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // must stop before changing duration

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.15f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.9f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 90;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var gradient = new Gradient();
            var keys = new GradientColorKey[Confetti.Length + 1];
            for (int i = 0; i < Confetti.Length; i++) keys[i] = new GradientColorKey(Confetti[i], i / (float)Confetti.Length);
            keys[Confetti.Length] = new GradientColorKey(accent, 1f);
            gradient.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 70) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-10f, 10f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = GrayBox.VertexColorUnlit;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            ps.Play();
        }
    }
}
