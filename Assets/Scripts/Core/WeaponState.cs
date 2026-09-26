using System;

namespace ArenaShooter.Core
{
    /// <summary>
    /// Runtime state of one weapon instance: ammo, fire cooldown and reload progress.
    /// The same instance moves between a player's hands and a pickup on the ground.
    /// </summary>
    public sealed class WeaponState
    {
        public WeaponStats Stats { get; }
        public int Magazine { get; private set; }
        public int Reserve { get; private set; }
        public bool IsReloading => _reloadRemaining > 0f;
        public float ReloadProgress => IsReloading ? 1f - _reloadRemaining / Stats.reloadTime : 0f;
        public bool IsCoolingDown => _cooldown > TimingEpsilon;
        public bool CanFire => !IsReloading && !IsCoolingDown && Magazine > 0;
        public bool IsEmpty => Magazine == 0 && !Stats.HasUnlimitedReserve && Reserve == 0;

        // Absorbs float error from summing per-frame deltas so shots land on the intended frame.
        private const float TimingEpsilon = 1e-4f;

        private float _cooldown;
        private float _reloadRemaining;

        public WeaponState(WeaponStats stats)
            : this(stats, stats.magazineSize, stats.HasUnlimitedReserve ? 0 : stats.maxReserve)
        {
        }

        public WeaponState(WeaponStats stats, int magazine, int reserve)
        {
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            Magazine = Clamp(magazine, 0, stats.magazineSize);
            Reserve = stats.HasUnlimitedReserve ? 0 : Clamp(reserve, 0, stats.maxReserve);
        }

        /// <summary>Consumes a round if the weapon is ready. Starts a reload automatically when the magazine runs dry.</summary>
        public bool TryFire()
        {
            if (!CanFire) return false;

            Magazine--;
            // Add rather than assign so leftover frame time carries over and cadence stays exact.
            _cooldown += Stats.fireInterval;
            if (Magazine == 0) TryStartReload();
            return true;
        }

        public bool TryStartReload()
        {
            if (IsReloading || Magazine >= Stats.magazineSize) return false;
            if (!Stats.HasUnlimitedReserve && Reserve <= 0) return false;

            _reloadRemaining = Stats.reloadTime;
            if (_reloadRemaining <= 0f) FinishReload();
            return true;
        }

        public void CancelReload() => _reloadRemaining = 0f;

        public void Tick(float deltaTime)
        {
            if (_cooldown > 0f) _cooldown -= deltaTime;

            if (IsReloading)
            {
                _reloadRemaining -= deltaTime;
                if (_reloadRemaining <= 0f) FinishReload();
            }
        }

        /// <summary>Adds spare rounds up to the reserve cap. Returns how many were accepted.</summary>
        public int AddReserve(int amount)
        {
            if (amount <= 0 || Stats.HasUnlimitedReserve) return 0;
            int accepted = Math.Min(amount, Stats.maxReserve - Reserve);
            Reserve += accepted;
            return accepted;
        }

        /// <summary>Removes up to <paramref name="amount"/> rounds (reserve first, then magazine). Returns how many were removed.</summary>
        public int RemoveAmmo(int amount)
        {
            if (amount <= 0) return 0;
            int fromReserve = Math.Min(amount, Reserve);
            Reserve -= fromReserve;
            int fromMagazine = Math.Min(amount - fromReserve, Magazine);
            Magazine -= fromMagazine;
            return fromReserve + fromMagazine;
        }

        private void FinishReload()
        {
            _reloadRemaining = 0f;
            int needed = Stats.magazineSize - Magazine;
            int loaded = Stats.HasUnlimitedReserve ? needed : Math.Min(needed, Reserve);
            Magazine += loaded;
            if (!Stats.HasUnlimitedReserve) Reserve -= loaded;
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}
