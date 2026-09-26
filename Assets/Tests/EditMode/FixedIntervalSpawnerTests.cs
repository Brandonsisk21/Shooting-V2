using NUnit.Framework;

namespace ArenaShooter.Core.Tests
{
    public class FixedIntervalSpawnerTests
    {
        [Test]
        public void SpawnsImmediatelyThenEveryInterval()
        {
            var spawner = new FixedIntervalSpawner(90f);
            Assert.IsTrue(spawner.Tick(0f), "Spawns at match start.");
            Assert.IsFalse(spawner.Tick(89f));
            Assert.IsTrue(spawner.Tick(1f));
            Assert.IsFalse(spawner.Tick(89.5f));
            Assert.IsTrue(spawner.Tick(0.5f));
        }

        [Test]
        public void TimerDoesNotDriftAtFrameRate()
        {
            var spawner = new FixedIntervalSpawner(90f);
            spawner.Tick(0f);
            int spawns = 0;
            for (int frame = 0; frame < 60 * 280; frame++) // 280s: spawns at 90, 180, 270
                if (spawner.Tick(1f / 60f)) spawns++;
            Assert.AreEqual(3, spawns);
        }
    }
}
