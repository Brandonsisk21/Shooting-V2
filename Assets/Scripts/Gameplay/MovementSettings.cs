using System;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>Classic Halo-style movement tuning (GDD 2.4). 1 unit = 1 meter.</summary>
    [Serializable]
    public class MovementSettings
    {
        [Tooltip("Top ground speed in m/s. Same in every direction; there is no sprint.")]
        public float moveSpeed = 6f;
        [Tooltip("How fast you reach top speed on the ground (m/s²).")]
        public float groundAcceleration = 50f;
        [Tooltip("How fast you stop on the ground with no input (m/s²).")]
        public float groundDeceleration = 50f;
        [Tooltip("Steering strength in the air (m/s²). ~30% of ground = moderate air control.")]
        public float airAcceleration = 15f;
        [Tooltip("Air strafing can't push you past this horizontal speed (m/s).")]
        public float maxAirSpeed = 6f;
        [Tooltip("Apex height of a jump in meters.")]
        public float jumpHeight = 1.3f;
        [Tooltip("Downward acceleration in m/s². Higher than real gravity for a snappier arc.")]
        public float gravity = 20f;
        [Tooltip("A jump pressed this long before landing still fires on landing (seconds).")]
        public float jumpBufferTime = 0.1f;

        public float JumpVelocity => Mathf.Sqrt(2f * gravity * jumpHeight);
    }
}
