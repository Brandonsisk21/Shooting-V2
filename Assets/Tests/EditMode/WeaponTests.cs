using NUnit.Framework;

namespace ArenaShooter.Core.Tests
{
    public class WeaponTests
    {
        private static int ShotsToKill(WeaponStats stats, params HitZone[] zones)
        {
            var health = new HealthModel(100f);
            for (int i = 0; i < zones.Length; i++)
                if (health.ApplyDamage(stats.DamageFor(zones[i])).Killed) return i + 1;
            return -1;
        }

        [Test]
        public void RifleKillsMatchGdd()
        {
            var rifle = WeaponStats.Rifle();
            Assert.AreEqual(3, ShotsToKill(rifle, HitZone.Head, HitZone.Head, HitZone.Body), "2 headshots + any shot");
            Assert.AreEqual(4, ShotsToKill(rifle, HitZone.Body, HitZone.Body, HitZone.Body, HitZone.Body), "4 body shots");
            Assert.AreEqual(3, ShotsToKill(rifle, HitZone.Head, HitZone.Head, HitZone.Head));
        }

        [Test]
        public void SniperKillsMatchGdd()
        {
            var sniper = WeaponStats.Sniper();
            Assert.AreEqual(1, ShotsToKill(sniper, HitZone.Head));
            Assert.AreEqual(2, ShotsToKill(sniper, HitZone.Body, HitZone.Body));
        }

        [Test]
        public void RifleFiresFiveShotsPerSecondAt60Fps()
        {
            var weapon = new WeaponState(WeaponStats.Rifle());
            int shots = 0;
            // Hold the trigger for exactly one second of 60 fps frames, starting ready to fire.
            for (int frame = 0; frame < 60; frame++)
            {
                if (weapon.TryFire()) shots++;
                weapon.Tick(1f / 60f);
            }
            Assert.AreEqual(5, shots);
        }

        [Test]
        public void CannotFireFasterThanInterval()
        {
            var weapon = new WeaponState(WeaponStats.Rifle());
            Assert.IsTrue(weapon.TryFire());
            weapon.Tick(0.1f);
            Assert.IsFalse(weapon.TryFire());
            weapon.Tick(0.1f);
            Assert.IsTrue(weapon.TryFire());
        }

        [Test]
        public void EmptyMagazineAutoReloadsFromUnlimitedReserve()
        {
            var weapon = new WeaponState(WeaponStats.Rifle());
            for (int i = 0; i < 30; i++)
            {
                Assert.IsTrue(weapon.TryFire(), $"shot {i}");
                weapon.Tick(0.2f);
            }
            Assert.AreEqual(0, weapon.Magazine);
            Assert.IsTrue(weapon.IsReloading);
            Assert.IsFalse(weapon.TryFire());

            weapon.Tick(2f);
            Assert.IsFalse(weapon.IsReloading);
            Assert.AreEqual(30, weapon.Magazine);
        }

        [Test]
        public void ReloadDrawsFromLimitedReserve()
        {
            var weapon = new WeaponState(WeaponStats.Sniper());
            Assert.AreEqual(4, weapon.Magazine);
            Assert.AreEqual(8, weapon.Reserve);

            weapon.TryFire();
            weapon.Tick(1f);
            Assert.IsTrue(weapon.TryStartReload());
            weapon.Tick(2.5f);

            Assert.AreEqual(4, weapon.Magazine);
            Assert.AreEqual(7, weapon.Reserve);
        }

        [Test]
        public void CannotReloadFullMagazineOrWithoutReserve()
        {
            var full = new WeaponState(WeaponStats.Sniper());
            Assert.IsFalse(full.TryStartReload());

            var dry = new WeaponState(WeaponStats.Sniper(), magazine: 1, reserve: 0);
            Assert.IsFalse(dry.TryStartReload());
            dry.TryFire();
            Assert.IsTrue(dry.IsEmpty);
            Assert.IsFalse(dry.IsReloading);
        }

        [Test]
        public void SniperEmptiesAfterTwelveShots()
        {
            var weapon = new WeaponState(WeaponStats.Sniper());
            int shots = 0;
            for (int i = 0; i < 1000 && !weapon.IsEmpty; i++)
            {
                if (weapon.TryFire()) shots++;
                weapon.Tick(0.1f);
            }
            Assert.AreEqual(12, shots);
        }
    }
}
