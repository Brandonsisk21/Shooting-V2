using ArenaShooter.Core;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>A confirmed hit on something with <see cref="Health"/>; drives hit markers and damage numbers.</summary>
    public readonly struct HitInfo
    {
        public readonly Health Target;
        public readonly HitZone Zone;
        public readonly DamageResult Damage;
        public readonly Vector3 Point;

        public HitInfo(Health target, HitZone zone, DamageResult damage, Vector3 point)
        {
            Target = target;
            Zone = zone;
            Damage = damage;
            Point = point;
        }
    }
}
