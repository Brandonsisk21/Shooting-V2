using System;
using System.Collections.Generic;

namespace ArenaShooter.Core
{
    public enum PickupOutcome
    {
        /// <summary>Nothing happened (e.g. already carrying this weapon with full reserve).</summary>
        None,
        /// <summary>Weapon went into a free slot.</summary>
        Added,
        /// <summary>Weapon replaced the held one, which was dropped.</summary>
        Swapped,
        /// <summary>Already carrying this weapon; took its ammo instead.</summary>
        AmmoTaken,
    }

    public readonly struct PickupResult
    {
        public readonly PickupOutcome Outcome;
        /// <summary>The weapon left behind on a swap, otherwise null.</summary>
        public readonly WeaponState Dropped;
        public readonly int AmmoTaken;

        public PickupResult(PickupOutcome outcome, WeaponState dropped = null, int ammoTaken = 0)
        {
            Outcome = outcome;
            Dropped = dropped;
            AmmoTaken = ammoTaken;
        }
    }

    /// <summary>
    /// A combatant's carried weapons (GDD 2.3.1): up to two slots, pick up only, and a weapon is
    /// dropped only when swapped for a different one. Shared by the player and (later) bots.
    /// </summary>
    public sealed class Loadout
    {
        public const int MaxSlots = 2;

        public float SwitchTime { get; set; }
        public IReadOnlyList<WeaponState> Slots => _slots;
        public int ActiveIndex { get; private set; }
        public WeaponState Active => _slots.Count > 0 ? _slots[ActiveIndex] : null;
        public bool IsSwitching => _switchRemaining > 0f;
        public bool CanFire => !IsSwitching && Active != null && Active.CanFire;

        private readonly List<WeaponState> _slots = new List<WeaponState>(MaxSlots);
        private float _switchRemaining;

        public event Action<WeaponState> ActiveChanged;

        public Loadout(float switchTime = 0.4f)
        {
            SwitchTime = switchTime;
        }

        public void Clear()
        {
            _slots.Clear();
            ActiveIndex = 0;
            _switchRemaining = 0f;
        }

        /// <summary>Gives a weapon directly (spawn loadout). Fails if both slots are full.</summary>
        public bool Give(WeaponState weapon)
        {
            if (weapon == null || _slots.Count >= MaxSlots) return false;
            _slots.Add(weapon);
            if (_slots.Count == 1) ActiveChanged?.Invoke(Active);
            return true;
        }

        public WeaponState Find(string weaponId)
        {
            foreach (var slot in _slots)
                if (slot.Stats.id == weaponId) return slot;
            return null;
        }

        /// <summary>Describes what picking up <paramref name="ground"/> would do, without doing it.</summary>
        public PickupOutcome Preview(WeaponState ground)
        {
            if (ground == null) return PickupOutcome.None;
            var owned = Find(ground.Stats.id);
            if (owned != null)
            {
                bool roomForAmmo = !owned.Stats.HasUnlimitedReserve && owned.Reserve < owned.Stats.maxReserve;
                return roomForAmmo && (ground.Magazine + ground.Reserve) > 0 ? PickupOutcome.AmmoTaken : PickupOutcome.None;
            }
            return _slots.Count < MaxSlots ? PickupOutcome.Added : PickupOutcome.Swapped;
        }

        /// <summary>
        /// Picks up a weapon from the ground. On <see cref="PickupOutcome.AmmoTaken"/> the rounds are
        /// removed from <paramref name="ground"/>, which stays on the ground if it still has any.
        /// </summary>
        public PickupResult Pickup(WeaponState ground)
        {
            switch (Preview(ground))
            {
                case PickupOutcome.AmmoTaken:
                {
                    var owned = Find(ground.Stats.id);
                    int room = owned.Stats.maxReserve - owned.Reserve;
                    int taken = ground.RemoveAmmo(room);
                    owned.AddReserve(taken);
                    return new PickupResult(PickupOutcome.AmmoTaken, ammoTaken: taken);
                }
                case PickupOutcome.Added:
                    _slots.Add(ground);
                    SetActive(_slots.Count - 1);
                    return new PickupResult(PickupOutcome.Added);
                case PickupOutcome.Swapped:
                {
                    var dropped = Active;
                    dropped.CancelReload();
                    _slots[ActiveIndex] = ground;
                    SetActive(ActiveIndex);
                    return new PickupResult(PickupOutcome.Swapped, dropped);
                }
                default:
                    return new PickupResult(PickupOutcome.None);
            }
        }

        public bool SwitchNext()
        {
            if (_slots.Count < 2) return false;
            SetActive((ActiveIndex + 1) % _slots.Count);
            return true;
        }

        public bool SwitchTo(int index)
        {
            if (index < 0 || index >= _slots.Count || index == ActiveIndex) return false;
            SetActive(index);
            return true;
        }

        public bool TryFire() => !IsSwitching && Active != null && Active.TryFire();

        public bool TryReload() => !IsSwitching && Active != null && Active.TryStartReload();

        public void Tick(float deltaTime)
        {
            if (_switchRemaining > 0f) _switchRemaining -= deltaTime;
            Active?.Tick(deltaTime);
        }

        private void SetActive(int index)
        {
            Active?.CancelReload();
            ActiveIndex = index;
            _switchRemaining = SwitchTime;
            ActiveChanged?.Invoke(Active);
        }
    }
}
