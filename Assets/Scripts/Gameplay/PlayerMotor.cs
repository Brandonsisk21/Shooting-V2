using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// CharacterController-based movement: no sprint, single fixed-height jump, strafing with
    /// moderate air control, hold-to-crouch. Input-agnostic: players, bots and network-driven
    /// grunts all use it, so crouching lowers the eyes, collision and hitboxes the same way for all.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        public MovementSettings settings = new MovementSettings();

        public Vector3 Velocity => _velocity;
        public bool IsGrounded => _controller.isGrounded;

        [Tooltip("Eye/aim pivot lowered while crouching (camera for the player, head for bots).")]
        public Transform eyePivot;
        [Tooltip("Body hitbox shortened while crouching.")]
        public CapsuleCollider bodyHitbox;
        [Tooltip("Head hitbox lowered while crouching.")]
        public Transform headHitbox;

        /// <summary>Whether this grunt is trying to crouch (input, AI or network).</summary>
        public bool WantsCrouch { get; set; }
        /// <summary>0 = standing, 1 = fully crouched (smoothed).</summary>
        public float CrouchAmount { get; private set; }
        public bool IsCrouched => CrouchAmount > 0.5f;

        // Hitbox layout (matches CombatantBody): standing body 1.3 m tall, head center at 1.55 m.
        private const float StandBodyHeight = 1.3f, CrouchBodyHeight = 0.85f;
        private const float StandHeadY = 1.55f, CrouchHeadY = 1.02f;

        private CharacterController _controller;
        private Vector3 _velocity;
        private float _jumpBufferedUntil = float.NegativeInfinity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        /// <param name="moveInput">x = strafe, y = forward, each in [-1, 1].</param>
        public void Move(Vector2 moveInput, bool jumpPressed, float deltaTime)
        {
            if (deltaTime <= 0f || !_controller.enabled) return;
            if (jumpPressed) _jumpBufferedUntil = Time.time + settings.jumpBufferTime;

            bool grounded = _controller.isGrounded;
            Vector3 wishDir = transform.right * moveInput.x + transform.forward * moveInput.y;
            wishDir.y = 0f;
            wishDir = Vector3.ClampMagnitude(wishDir, 1f);

            var horizontal = new Vector3(_velocity.x, 0f, _velocity.z);
            if (grounded)
            {
                float rate = wishDir.sqrMagnitude > 0f ? settings.groundAcceleration : settings.groundDeceleration;
                float speed = Mathf.Lerp(settings.moveSpeed, settings.crouchSpeed, CrouchAmount);
                horizontal = Vector3.MoveTowards(horizontal, wishDir * speed, rate * deltaTime);
            }
            else if (wishDir.sqrMagnitude > 0f)
            {
                // Steer toward the wish velocity; with no input, air momentum is kept.
                horizontal = Vector3.MoveTowards(horizontal, wishDir * settings.maxAirSpeed, settings.airAcceleration * deltaTime);
            }

            float vertical = _velocity.y;
            if (grounded && vertical < 0f) vertical = -2f; // keep contact on slopes and step-downs

            if (grounded && Time.time <= _jumpBufferedUntil)
            {
                vertical = settings.JumpVelocity;
                _jumpBufferedUntil = float.NegativeInfinity;
            }
            else
            {
                vertical -= settings.gravity * deltaTime;
            }

            _velocity = new Vector3(horizontal.x, vertical, horizontal.z);
            CollisionFlags flags = _controller.Move(_velocity * deltaTime);

            if ((flags & CollisionFlags.Above) != 0 && _velocity.y > 0f) _velocity.y = 0f;
            if ((flags & CollisionFlags.Sides) != 0)
            {
                // Don't keep pushing into walls; keep only the movement the controller actually made.
                Vector3 actual = _controller.velocity;
                _velocity.x = actual.x;
                _velocity.z = actual.z;
            }
        }

        private void Update()
        {
            // Crouch transition runs every frame for everyone (including network-driven grunts).
            float target = WantsCrouch ? 1f : 0f;
            if (target < CrouchAmount && !HasHeadroom()) target = CrouchAmount; // something above: stay down
            float next = Mathf.MoveTowards(CrouchAmount, target, settings.crouchTransitionSpeed * Time.deltaTime);
            if (!Mathf.Approximately(next, CrouchAmount) || _poseDirty) ApplyCrouch(next);
        }

        private bool _poseDirty = true;

        private void ApplyCrouch(float amount)
        {
            CrouchAmount = amount;
            _poseDirty = false;

            float height = Mathf.Lerp(settings.standHeight, settings.crouchHeight, amount);
            _controller.height = height;
            _controller.center = new Vector3(0f, height * 0.5f, 0f);

            if (eyePivot != null)
            {
                var p = eyePivot.localPosition;
                eyePivot.localPosition = new Vector3(p.x, Mathf.Lerp(settings.standEyeHeight, settings.crouchEyeHeight, amount), p.z);
            }
            if (bodyHitbox != null)
            {
                float h = Mathf.Lerp(StandBodyHeight, CrouchBodyHeight, amount);
                bodyHitbox.height = h;
                bodyHitbox.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            }
            if (headHitbox != null) headHitbox.localPosition = new Vector3(0f, Mathf.Lerp(StandHeadY, CrouchHeadY, amount), 0f);
        }

        /// <summary>Room to stand up? Casts upward from the crouched top, ignoring ourselves and triggers.</summary>
        private bool HasHeadroom()
        {
            float radius = _controller.radius * 0.9f;
            Vector3 start = transform.position + Vector3.up * (settings.crouchHeight - radius);
            float distance = settings.standHeight - settings.crouchHeight;
            foreach (var hit in Physics.SphereCastAll(start, radius, Vector3.up, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.collider is CharacterController) continue; // other grunts don't pin us down
                return false;
            }
            return true;
        }

        /// <summary>Instant push (grenade blasts). Adds to the current velocity.</summary>
        public void AddImpulse(Vector3 velocityChange)
        {
            if (!_controller.enabled) return;
            _velocity += velocityChange;
        }

        /// <summary>For bodies moved by the network rather than by Move(): report their velocity (animation).</summary>
        public void SetExternalVelocity(Vector3 velocity) => _velocity = velocity;

        /// <summary>Turns the body's collision on/off (off while dead so corpses don't block anyone).</summary>
        public void SetCollision(bool enabled)
        {
            _controller.enabled = enabled;
            if (!enabled) _velocity = Vector3.zero;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = true;
            _velocity = Vector3.zero;
            WantsCrouch = false;
            ApplyCrouch(0f); // spawn standing
        }
    }
}
