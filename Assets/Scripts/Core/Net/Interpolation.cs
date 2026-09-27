using System;
using System.Collections.Generic;

namespace ArenaShooter.Core.Net
{
    public struct Pose
    {
        public double Time;
        public V3 Position;
        public float Yaw;
        public float Pitch;
    }

    /// <summary>
    /// Smooths other players' movement: keeps recent snapshots and samples between them a little
    /// in the past, so motion looks continuous even though updates arrive ~20 times a second.
    /// </summary>
    public sealed class InterpolationBuffer
    {
        private readonly List<Pose> _poses = new List<Pose>();
        private readonly int _capacity;

        /// <summary>How far past the newest pose we keep extrapolating before freezing (seconds).</summary>
        public double MaxExtrapolation = 0.12;

        public int Count => _poses.Count;

        public InterpolationBuffer(int capacity = 32)
        {
            _capacity = capacity;
        }

        public void Clear() => _poses.Clear();

        /// <summary>Adds a pose. Out-of-order or duplicate (older) poses are ignored.</summary>
        public void Add(Pose pose)
        {
            if (_poses.Count > 0 && pose.Time <= _poses[_poses.Count - 1].Time) return;
            _poses.Add(pose);
            if (_poses.Count > _capacity) _poses.RemoveAt(0);
        }

        public bool TrySample(double time, out Pose result)
        {
            result = default;
            if (_poses.Count == 0) return false;

            if (time <= _poses[0].Time)
            {
                result = _poses[0];
                return true;
            }

            for (int i = 1; i < _poses.Count; i++)
            {
                if (time > _poses[i].Time) continue;
                result = Blend(_poses[i - 1], _poses[i], time);
                return true;
            }

            // Past the newest pose: carry on briefly with the last known velocity, then hold.
            var last = _poses[_poses.Count - 1];
            if (_poses.Count >= 2)
            {
                double t = Math.Min(time, last.Time + MaxExtrapolation);
                result = Blend(_poses[_poses.Count - 2], last, t);
                return true;
            }
            result = last;
            return true;
        }

        private static Pose Blend(Pose a, Pose b, double time)
        {
            double span = b.Time - a.Time;
            float t = span <= 1e-9 ? 1f : (float)((time - a.Time) / span);
            return new Pose
            {
                Time = time,
                Position = V3.Lerp(a.Position, b.Position, t),
                Yaw = LerpAngle(a.Yaw, b.Yaw, t),
                Pitch = a.Pitch + (b.Pitch - a.Pitch) * t,
            };
        }

        /// <summary>Interpolates angles in degrees the short way around.</summary>
        public static float LerpAngle(float a, float b, float t)
        {
            float delta = ((b - a) % 360f + 540f) % 360f - 180f;
            return a + delta * t;
        }
    }

    /// <summary>
    /// Estimates the host's clock from snapshot timestamps. Adapts quickly when the host seems
    /// ahead (a packet arrived faster than before) and slowly when behind (a late packet), so
    /// network jitter doesn't make everyone stutter.
    /// </summary>
    public sealed class ClockSync
    {
        private double _offset;
        private bool _hasSample;

        public bool IsSynced => _hasSample;

        public void OnHostTime(double hostTime, double localTime)
        {
            double sample = hostTime - localTime;
            if (!_hasSample)
            {
                _offset = sample;
                _hasSample = true;
                return;
            }
            double rate = sample > _offset ? 0.5 : 0.02;
            _offset += (sample - _offset) * rate;
        }

        public double HostTime(double localTime) => localTime + _offset;
    }
}
