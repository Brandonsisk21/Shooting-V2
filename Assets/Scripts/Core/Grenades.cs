using System;

namespace ArenaShooter.Core
{
    /// <summary>
    /// Grenade tuning (GDD 2.2.1). A thrown grenade bounces, then explodes after a fixed fuse;
    /// damage is full inside the kill radius and fades to nothing at the blast radius.
    /// </summary>
    [Serializable]
    public class GrenadeStats
    {
        public float maxDamage = 120f;
        /// <summary>Full damage (a guaranteed kill at 100 HP) inside this radius (m).</summary>
        public float killRadius = 1.5f;
        /// <summary>No damage beyond this radius (m).</summary>
        public float blastRadius = 5f;
        /// <summary>Seconds from throw to explosion.</summary>
        public float fuse = 2f;
        /// <summary>Throw speed (m/s) along the aim, plus a little upward lift.</summary>
        public float throwSpeed = 16f;
        public float throwLift = 0.18f;
        /// <summary>Minimum seconds between throws.</summary>
        public float throwCooldown = 0.8f;
        /// <summary>Knockback speed (m/s) at the center of the blast.</summary>
        public float knockback = 7f;

        public int startCount = 2;
        public int maxCount = 4;
        /// <summary>Grenades per map pickup.</summary>
        public int pickupAmount = 2;
        /// <summary>Seconds before a taken map pickup comes back.</summary>
        public float pickupRespawn = 30f;

        /// <summary>Damage to something <paramref name="distance"/> meters from the blast (before walls are considered).</summary>
        public float DamageAt(float distance)
        {
            if (distance >= blastRadius) return 0f;
            if (distance <= killRadius) return maxDamage;
            float t = (distance - killRadius) / (blastRadius - killRadius);
            return maxDamage * (1f - t);
        }

        /// <summary>0..1 blast strength at a distance (for knockback and screen shake).</summary>
        public float Falloff(float distance) => distance >= blastRadius ? 0f : 1f - Math.Max(0f, distance) / blastRadius;
    }

    /// <summary>How many grenades a grunt carries.</summary>
    public sealed class GrenadePouch
    {
        public int Count { get; private set; }
        public int Max { get; }

        public GrenadePouch(int start, int max)
        {
            Max = Math.Max(0, max);
            Count = Math.Max(0, Math.Min(start, Max));
        }

        public bool IsFull => Count >= Max;

        public bool TryUse()
        {
            if (Count <= 0) return false;
            Count--;
            return true;
        }

        /// <summary>Adds up to <paramref name="amount"/> without exceeding the max. Returns how many were taken.</summary>
        public int Add(int amount)
        {
            int taken = Math.Max(0, Math.Min(amount, Max - Count));
            Count += taken;
            return taken;
        }

        public void Reset(int start) => Count = Math.Max(0, Math.Min(start, Max));

        /// <summary>Online: the host's count for this grunt.</summary>
        public void Set(int count) => Count = Math.Max(0, Math.Min(count, Max));
    }
}
