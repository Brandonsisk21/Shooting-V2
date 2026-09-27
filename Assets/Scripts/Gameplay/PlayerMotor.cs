using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// CharacterController-based movement: no sprint, single fixed-height jump, strafing with
    /// moderate air control. Input-agnostic so bots can drive it later.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        public MovementSettings settings = new MovementSettings();

        public Vector3 Velocity => _velocity;
        public bool IsGrounded => _controller.isGrounded;

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
                horizontal = Vector3.MoveTowards(horizontal, wishDir * settings.moveSpeed, rate * deltaTime);
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
        }
    }
}
