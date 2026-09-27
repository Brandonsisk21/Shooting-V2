using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Cartoon life for a Space Grunt (GDD 5.1): a bouncy walk with swinging legs, a bobble-head
    /// wobble, arms that follow the aim so the blaster points where they look, and on death the
    /// helmet pops off and they burst into confetti.
    /// </summary>
    public class GruntVisual : MonoBehaviour
    {
        public Transform visual;
        /// <summary>The aim pitch pivot (bots' head); the arms copy its pitch.</summary>
        public Transform aim;
        public Transform armsPivot;
        public Transform legL, legR;
        /// <summary>Parts that bobble together (head and helmet).</summary>
        public Transform[] headParts = new Transform[0];
        /// <summary>The helmet part that pops off on death (3D model), if any.</summary>
        public Transform helmet;
        public Color color = Color.white;

        private static readonly Color[] Confetti =
        {
            new Color(1f, 0.3f, 0.35f), new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.9f, 1f),
            new Color(0.5f, 1f, 0.4f), new Color(1f, 0.5f, 0.85f), Color.white,
        };

        private PlayerMotor _motor;
        private Vector3 _visualBase;
        private Quaternion[] _headBase;
        private float _walkPhase;

        private void Start()
        {
            _motor = GetComponent<PlayerMotor>();
            _visualBase = visual.localPosition;
            _headBase = new Quaternion[headParts.Length];
            for (int i = 0; i < headParts.Length; i++)
                if (headParts[i] != null) _headBase[i] = headParts[i].localRotation;
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
            var bobble = Quaternion.Euler(Mathf.Sin(_walkPhase * 0.9f) * wobble * 0.6f, 0f, Mathf.Sin(_walkPhase * 0.5f + 1f) * wobble);
            for (int i = 0; i < headParts.Length; i++)
                if (headParts[i] != null) headParts[i].localRotation = _headBase[i] * bobble;

            // Stubby legs swing opposite each other while walking.
            float swing = Mathf.Sin(_walkPhase) * 32f * speed01;
            if (legL != null) legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
            if (legR != null) legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);

            // Arms (and the blaster in them) tilt with the aim.
            if (armsPivot != null && aim != null)
            {
                float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, aim.localEulerAngles.x), -60f, 60f);
                armsPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
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
            GameObject popped;
            if (this.helmet != null)
            {
                // Copy of the 3D helmet, re-centered so it tumbles around the bubble's middle.
                popped = new GameObject("PoppedHelmet");
                popped.transform.SetPositionAndRotation(this.helmet.position + this.helmet.up * 0.27f, this.helmet.rotation);
                var copy = Instantiate(this.helmet.gameObject, popped.transform);
                copy.transform.localPosition = Vector3.down * 0.27f;
                copy.transform.localRotation = Quaternion.identity;
                foreach (var r in copy.GetComponentsInChildren<Renderer>()) r.enabled = true;
            }
            else
            {
                Transform head = headParts.Length > 0 && headParts[0] != null ? headParts[0] : transform;
                popped = new GameObject("PoppedHelmet");
                popped.transform.SetPositionAndRotation(head.position + Vector3.up * 0.25f, head.rotation);
                var bubble = GrayBox.Visual(PrimitiveType.Sphere, "Bubble", popped.transform, Vector3.zero, Vector3.one * 0.62f, Color.white);
                bubble.GetComponent<Renderer>().sharedMaterial = GrayBox.Glass(new Color(0.75f, 0.95f, 1f, 0.35f));
                GrayBox.Visual(PrimitiveType.Cylinder, "Rim", popped.transform, new Vector3(0f, -0.25f, 0f), new Vector3(0.58f, 0.03f, 0.58f), color);
            }
            popped.layer = 2; // Ignore Raycast: shots pass through the flying helmet

            popped.AddComponent<SphereCollider>().radius = 0.3f;
            var body = popped.AddComponent<Rigidbody>();
            body.mass = 0.3f;
            // An impulse rather than setting .velocity (renamed linearVelocity in Unity 6).
            body.AddForce(Vector3.up * Random.Range(5f, 7f) + Random.insideUnitSphere * 2f, ForceMode.VelocityChange);
            body.angularVelocity = Random.insideUnitSphere * 10f;
            Destroy(popped, 4f);
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
