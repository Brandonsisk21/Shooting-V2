namespace ArenaShooter.Core
{
    /// <summary>
    /// Fires once at start, then every <see cref="Interval"/> seconds regardless of anything else
    /// (GDD 2.3: sniper spawns on a fixed 90s timer, independent of pickup state).
    /// </summary>
    public sealed class FixedIntervalSpawner
    {
        public float Interval { get; }
        public float TimeUntilNext { get; private set; }

        private bool _started;

        public FixedIntervalSpawner(float interval)
        {
            Interval = interval;
        }

        /// <summary>Advances time. Returns true when a spawn is due this tick.</summary>
        public bool Tick(float deltaTime)
        {
            if (!_started)
            {
                _started = true;
                TimeUntilNext = Interval;
                return true;
            }

            TimeUntilNext -= deltaTime;
            if (TimeUntilNext > 0f) return false;

            TimeUntilNext += Interval;
            return true;
        }
    }
}
