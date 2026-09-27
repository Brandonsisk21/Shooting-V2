using ArenaShooter.Core;

namespace ArenaShooter.Gameplay
{
    /// <summary>Who dealt damage and how, for kill credit, the kill feed and bot reactions.</summary>
    public readonly struct DamageSource
    {
        /// <summary>The attacker, or null for self-inflicted/environment damage.</summary>
        public readonly Combatant Instigator;
        public readonly string WeaponId;
        public readonly HitZone Zone;

        public DamageSource(Combatant instigator, string weaponId, HitZone zone)
        {
            Instigator = instigator;
            WeaponId = weaponId;
            Zone = zone;
        }
    }
}
