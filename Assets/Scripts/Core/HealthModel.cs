using System;

namespace ArenaShooter.Core
{
    public readonly struct DamageResult
    {
        public readonly float Applied;
        public readonly bool Killed;

        public DamageResult(float applied, bool killed)
        {
            Applied = applied;
            Killed = killed;
        }

        public static DamageResult None => new DamageResult(0f, false);
    }

    /// <summary>
    /// Health with Halo-style regeneration (GDD 2.1): after <see cref="RegenDelay"/> seconds without
    /// taking damage, health refills at <see cref="RegenRate"/> HP/s. Any damage restarts the delay.
    /// </summary>
    public sealed class HealthModel
    {
        public float Max { get; }
        public float RegenDelay { get; set; }
        public float RegenRate { get; set; }
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;
        public float Fraction => Max > 0f ? Current / Max : 0f;

        private float _timeSinceDamage;

        public event Action<DamageResult> Damaged;
        public event Action Died;

        public HealthModel(float max = 100f, float regenDelay = 5f, float regenRate = 25f)
        {
            if (max <= 0f) throw new ArgumentOutOfRangeException(nameof(max));
            Max = max;
            RegenDelay = regenDelay;
            RegenRate = regenRate;
            Current = max;
        }

        public DamageResult ApplyDamage(float amount)
        {
            if (IsDead || amount <= 0f) return DamageResult.None;

            float applied = Math.Min(amount, Current);
            Current -= applied;
            _timeSinceDamage = 0f;

            var result = new DamageResult(applied, IsDead);
            Damaged?.Invoke(result);
            if (result.Killed) Died?.Invoke();
            return result;
        }

        public void Tick(float deltaTime)
        {
            if (IsDead || deltaTime <= 0f) return;

            float before = _timeSinceDamage;
            _timeSinceDamage += deltaTime;
            if (Current >= Max || _timeSinceDamage < RegenDelay) return;

            // Only regen for the part of this tick that falls after the delay expired.
            float regenTime = Math.Min(deltaTime, _timeSinceDamage - Math.Max(before, RegenDelay));
            Current = Math.Min(Max, Current + RegenRate * regenTime);
        }

        /// <summary>Overwrites current health (online clients mirroring the host). Doesn't raise events.</summary>
        public void SetCurrent(float value)
        {
            Current = Math.Max(0f, Math.Min(Max, value));
        }

        public void Reset()
        {
            Current = Max;
            _timeSinceDamage = 0f;
        }
    }
}
