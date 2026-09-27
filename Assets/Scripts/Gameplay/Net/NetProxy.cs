using System;
using ArenaShooter.Core.Net;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Moves a grunt from network updates: buffers poses and plays them back slightly in the past
    /// (interpolation), so other players move smoothly between updates. Also feeds the body's
    /// walk animation with the resulting velocity.
    /// </summary>
    public class NetProxy : MonoBehaviour
    {
        public Transform eyes;
        [Tooltip("How far behind the newest update we render (seconds). Covers ~2 missed snapshots.")]
        public double interpolationDelay = 0.1;

        /// <summary>Clock the pose timestamps are in (host time on clients, local time on the host).</summary>
        public Func<double> Clock = () => Time.timeAsDouble;

        private readonly InterpolationBuffer _buffer = new InterpolationBuffer();
        private PlayerMotor _motor;
        private Vector3 _lastPosition;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _lastPosition = transform.position;
        }

        public void AddPose(double time, Vector3 position, float yaw, float pitch)
        {
            _buffer.Add(new ArenaShooter.Core.Net.Pose { Time = time, Position = new V3(position.x, position.y, position.z), Yaw = yaw, Pitch = pitch });
        }

        /// <summary>Jump straight to a pose (spawns) without interpolating from the old spot.</summary>
        public void Snap(Vector3 position, float yaw)
        {
            _buffer.Clear();
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            _lastPosition = position;
        }

        private void LateUpdate()
        {
            if (!_buffer.TrySample(Clock() - interpolationDelay, out var pose)) return;

            var position = new Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z);
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, pose.Yaw, 0f));
            if (eyes != null) eyes.localRotation = Quaternion.Euler(pose.Pitch, 0f, 0f);

            float dt = Time.deltaTime;
            if (_motor != null && dt > 0f) _motor.SetExternalVelocity((position - _lastPosition) / dt);
            _lastPosition = position;
        }
    }
}
