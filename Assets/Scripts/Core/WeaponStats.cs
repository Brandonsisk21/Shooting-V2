using System;

namespace ArenaShooter.Core
{
    /// <summary>
    /// Tunable per-weapon numbers. Defaults come from the GDD (2.2); tweak them in the inspector
    /// rather than in code.
    /// </summary>
    [Serializable]
    public class WeaponStats
    {
        /// <summary>Marks unlimited reserve ammo in <see cref="maxReserve"/>.</summary>
        public const int Unlimited = -1;

        public string id = "weapon";
        public string displayName = "Weapon";
        public float headDamage = 40f;
        public float bodyDamage = 25f;
        /// <summary>Minimum seconds between shots.</summary>
        public float fireInterval = 0.2f;
        public int magazineSize = 30;
        /// <summary>Max spare rounds carried outside the magazine, or <see cref="Unlimited"/>.</summary>
        public int maxReserve = Unlimited;
        public float reloadTime = 2f;
        /// <summary>Hitscan range in meters.</summary>
        public float range = 150f;
        /// <summary>Scope magnification. 1 means the weapon has no scope.</summary>
        public float zoom = 1f;

        public bool HasScope => zoom > 1f;
        public bool HasUnlimitedReserve => maxReserve < 0;

        public float DamageFor(HitZone zone) => zone == HitZone.Head ? headDamage : bodyDamage;

        public static WeaponStats Rifle() => new WeaponStats
        {
            id = "rifle",
            displayName = "Rifle",
            headDamage = 40f,
            bodyDamage = 25f,
            fireInterval = 0.2f,
            magazineSize = 30,
            maxReserve = Unlimited,
            reloadTime = 2f,
            range = 150f,
            zoom = 1f,
        };

        public static WeaponStats Sniper() => new WeaponStats
        {
            id = "sniper",
            displayName = "Sniper",
            headDamage = 100f,
            bodyDamage = 50f,
            fireInterval = 0.8f,
            magazineSize = 4,
            maxReserve = 8,
            reloadTime = 2.5f,
            range = 500f,
            zoom = 2f,
        };
    }
}
