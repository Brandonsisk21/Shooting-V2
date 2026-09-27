using NUnit.Framework;

namespace ArenaShooter.Core.Tests
{
    public class GrenadeTests
    {
        [Test]
        public void DirectHitDoes60AndTwoKill()
        {
            var g = new GrenadeStats();
            Assert.AreEqual(60f, g.DamageAt(0f));
            Assert.AreEqual(g.maxDamage, g.DamageAt(g.killRadius));
            var health = new HealthModel(100f);
            Assert.IsFalse(health.ApplyDamage(g.DamageAt(0f)).Killed, "one grenade alone doesn't kill a full-health grunt");
            Assert.IsTrue(health.ApplyDamage(g.DamageAt(0f)).Killed);
        }

        [Test]
        public void DamageFadesToZeroAtBlastRadius()
        {
            var g = new GrenadeStats();
            float mid = (g.killRadius + g.blastRadius) / 2f;
            Assert.AreEqual(g.maxDamage / 2f, g.DamageAt(mid), 1e-3f);
            Assert.AreEqual(0f, g.DamageAt(g.blastRadius));
            Assert.AreEqual(0f, g.DamageAt(50f));
        }

        [Test]
        public void DamageNeverIncreasesWithDistance()
        {
            var g = new GrenadeStats();
            float last = float.MaxValue;
            for (float d = 0f; d <= 6f; d += 0.25f)
            {
                float dmg = g.DamageAt(d);
                Assert.LessOrEqual(dmg, last);
                last = dmg;
            }
        }

        [Test]
        public void PouchStartsWithTwoAndCapsAtMax()
        {
            var g = new GrenadeStats();
            var pouch = new GrenadePouch(g.startCount, g.maxCount);
            Assert.AreEqual(2, pouch.Count);
            Assert.AreEqual(2, pouch.Add(g.pickupAmount));
            Assert.IsTrue(pouch.IsFull);
            Assert.AreEqual(0, pouch.Add(2), "full pouch takes nothing");
        }

        [Test]
        public void ThrowingUsesGrenadesUntilEmpty()
        {
            var pouch = new GrenadePouch(2, 4);
            Assert.IsTrue(pouch.TryUse());
            Assert.IsTrue(pouch.TryUse());
            Assert.IsFalse(pouch.TryUse());
            pouch.Reset(2);
            Assert.AreEqual(2, pouch.Count);
        }
    }
}
