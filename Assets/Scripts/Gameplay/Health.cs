using System;
using ArenaShooter.Core;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>Unity wrapper around <see cref="HealthModel"/> (100 HP, Halo-style regen).</summary>
    public class Health : MonoBehaviour
    {
        public float maxHealth = 100f;
        [Tooltip("Seconds without damage before regen starts.")]
        public float regenDelay = 5f;
        [Tooltip("HP per second once regen starts.")]
        public float regenRate = 25f;

        public HealthModel Model { get; private set; }
        public float Current => Model.Current;
        public bool IsDead => Model.IsDead;

        public event Action<Health, DamageResult, DamageSource> Damaged;
        public event Action<Health, DamageSource> Died;
        /// <summary>Any grunt took damage (e.g. the HUD shows a hit marker for your grenade hits).</summary>
        public static event Action<Health, DamageResult, DamageSource> AnyDamaged;

        private void Awake()
        {
            Model = new HealthModel(maxHealth, regenDelay, regenRate);
        }

        private void Update()
        {
            // Copy every frame so regen can be tuned live in the inspector.
            Model.RegenDelay = regenDelay;
            Model.RegenRate = regenRate;
            Model.Tick(Time.deltaTime);
        }

        public DamageResult TakeDamage(float amount, DamageSource source = default)
        {
            DamageResult result = Model.ApplyDamage(amount);
            if (result.Applied > 0f)
            {
                Damaged?.Invoke(this, result, source);
                AnyDamaged?.Invoke(this, result, source);
            }
            if (result.Killed) Died?.Invoke(this, source);
            return result;
        }

        public void ResetHealth() => Model.Reset();

        /// <summary>Online: snap to the host's value (no events).</summary>
        public void SetCurrent(float value) => Model.SetCurrent(value);
    }
}
