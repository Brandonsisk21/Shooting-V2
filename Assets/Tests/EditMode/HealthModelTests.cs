using NUnit.Framework;

namespace ArenaShooter.Core.Tests
{
    public class HealthModelTests
    {
        [Test]
        public void StartsAtMax()
        {
            var health = new HealthModel(100f);
            Assert.AreEqual(100f, health.Current);
            Assert.IsFalse(health.IsDead);
        }

        [Test]
        public void DamageReducesHealthAndKillsAtZero()
        {
            var health = new HealthModel(100f);
            bool died = false;
            health.Died += () => died = true;

            Assert.IsFalse(health.ApplyDamage(60f).Killed);
            var result = health.ApplyDamage(60f);

            Assert.IsTrue(result.Killed);
            Assert.AreEqual(40f, result.Applied, "Only the remaining health counts as applied damage.");
            Assert.IsTrue(died);
            Assert.AreEqual(0f, health.Current);
        }

        [Test]
        public void DeadTargetsIgnoreFurtherDamage()
        {
            var health = new HealthModel(100f);
            health.ApplyDamage(100f);
            Assert.AreEqual(0f, health.ApplyDamage(50f).Applied);
        }

        [Test]
        public void NoRegenBeforeDelay()
        {
            var health = new HealthModel(100f, regenDelay: 5f, regenRate: 25f);
            health.ApplyDamage(50f);
            health.Tick(4.9f);
            Assert.AreEqual(50f, health.Current);
        }

        [Test]
        public void RegensOnlyForTimePastTheDelay()
        {
            var health = new HealthModel(100f, regenDelay: 5f, regenRate: 25f);
            health.ApplyDamage(50f);
            health.Tick(6f); // 1s past the delay
            Assert.AreEqual(75f, health.Current, 1e-4f);
        }

        [Test]
        public void RegenIsFrameRateIndependent()
        {
            var health = new HealthModel(100f, regenDelay: 5f, regenRate: 25f);
            health.ApplyDamage(90f);
            for (int i = 0; i < 360; i++) health.Tick(1f / 60f); // 6s
            Assert.AreEqual(35f, health.Current, 1e-2f);
        }

        [Test]
        public void RegenCapsAtMax()
        {
            var health = new HealthModel(100f, regenDelay: 5f, regenRate: 25f);
            health.ApplyDamage(10f);
            health.Tick(60f);
            Assert.AreEqual(100f, health.Current);
        }

        [Test]
        public void DamageRestartsTheRegenDelay()
        {
            var health = new HealthModel(100f, regenDelay: 5f, regenRate: 25f);
            health.ApplyDamage(20f);
            health.Tick(4f);
            health.ApplyDamage(20f);
            health.Tick(4f);
            Assert.AreEqual(60f, health.Current);
        }

        [Test]
        public void DeadDoesNotRegen()
        {
            var health = new HealthModel(100f, regenDelay: 5f, regenRate: 25f);
            health.ApplyDamage(100f);
            health.Tick(30f);
            Assert.IsTrue(health.IsDead);
        }
    }
}
