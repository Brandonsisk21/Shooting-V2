using ArenaShooter.Core;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>Marks a collider as a damageable zone (head or body) of the nearest parent <see cref="Health"/>.</summary>
    [RequireComponent(typeof(Collider))]
    public class Hitbox : MonoBehaviour
    {
        public HitZone zone = HitZone.Body;

        public Health Owner
        {
            get
            {
                if (_owner == null) _owner = GetComponentInParent<Health>();
                return _owner;
            }
        }

        private Health _owner;
    }
}
