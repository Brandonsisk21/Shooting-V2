using NUnit.Framework;

namespace ArenaShooter.Core.Tests
{
    public class LoadoutTests
    {
        private static Loadout SpawnLoadout()
        {
            var loadout = new Loadout(switchTime: 0.4f);
            loadout.Give(new WeaponState(WeaponStats.Rifle()));
            return loadout;
        }

        [Test]
        public void SpawnsWithRifleOnly()
        {
            var loadout = SpawnLoadout();
            Assert.AreEqual(1, loadout.Slots.Count);
            Assert.AreEqual("rifle", loadout.Active.Stats.id);
        }

        [Test]
        public void PickingUpSniperFillsFreeSlotAndEquipsIt()
        {
            var loadout = SpawnLoadout();
            var result = loadout.Pickup(new WeaponState(WeaponStats.Sniper()));

            Assert.AreEqual(PickupOutcome.Added, result.Outcome);
            Assert.IsNull(result.Dropped);
            Assert.AreEqual(2, loadout.Slots.Count);
            Assert.AreEqual("sniper", loadout.Active.Stats.id);
        }

        [Test]
        public void PickingUpOwnedWeaponTakesAmmoOnly()
        {
            var loadout = SpawnLoadout();
            var mine = new WeaponState(WeaponStats.Sniper(), magazine: 4, reserve: 2);
            loadout.Pickup(mine);

            var ground = new WeaponState(WeaponStats.Sniper());
            var result = loadout.Pickup(ground);

            Assert.AreEqual(PickupOutcome.AmmoTaken, result.Outcome);
            Assert.AreEqual(6, result.AmmoTaken);
            Assert.AreEqual(8, mine.Reserve);
            Assert.AreEqual(2, loadout.Slots.Count);
            Assert.AreEqual(6, ground.Magazine + ground.Reserve, "Leftover ammo stays on the ground.");
        }

        [Test]
        public void PickingUpOwnedWeaponWithFullReserveDoesNothing()
        {
            var loadout = SpawnLoadout();
            loadout.Pickup(new WeaponState(WeaponStats.Sniper()));
            Assert.AreEqual(PickupOutcome.None, loadout.Preview(new WeaponState(WeaponStats.Sniper())));
            Assert.AreEqual(PickupOutcome.None, loadout.Preview(new WeaponState(WeaponStats.Rifle())), "Rifle reserve is unlimited.");
        }

        [Test]
        public void FullSlotsSwapHeldWeaponAndDropIt()
        {
            var loadout = SpawnLoadout();
            loadout.Pickup(new WeaponState(WeaponStats.Sniper()));
            loadout.SwitchTo(0); // holding rifle

            var shotgunStats = new WeaponStats { id = "test_other", displayName = "Other" };
            var result = loadout.Pickup(new WeaponState(shotgunStats));

            Assert.AreEqual(PickupOutcome.Swapped, result.Outcome);
            Assert.AreEqual("rifle", result.Dropped.Stats.id);
            Assert.AreEqual(2, loadout.Slots.Count);
            Assert.AreEqual("test_other", loadout.Active.Stats.id);
            Assert.IsNotNull(loadout.Find("sniper"));
        }

        [Test]
        public void SwitchingBlocksFireForSwitchTime()
        {
            var loadout = SpawnLoadout();
            loadout.Pickup(new WeaponState(WeaponStats.Sniper()));
            loadout.Tick(0.5f);

            Assert.IsTrue(loadout.SwitchNext());
            Assert.AreEqual("rifle", loadout.Active.Stats.id);
            Assert.IsFalse(loadout.TryFire());
            loadout.Tick(0.4f);
            Assert.IsTrue(loadout.TryFire());
        }

        [Test]
        public void SwitchingCancelsReload()
        {
            var loadout = SpawnLoadout();
            loadout.Pickup(new WeaponState(WeaponStats.Sniper()));
            loadout.Tick(0.5f);
            loadout.TryFire();
            loadout.Tick(1f);
            Assert.IsTrue(loadout.TryReload());

            var sniper = loadout.Active;
            loadout.SwitchNext();
            Assert.IsFalse(sniper.IsReloading);
            Assert.AreEqual(3, sniper.Magazine);
        }

        [Test]
        public void CannotSwitchWithOneWeapon()
        {
            Assert.IsFalse(SpawnLoadout().SwitchNext());
        }
    }
}
