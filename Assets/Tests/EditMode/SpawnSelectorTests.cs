using System;
using NUnit.Framework;

namespace ArenaShooter.Core.Tests
{
    public class SpawnSelectorTests
    {
        private static readonly GroundPoint[] Spawns =
        {
            new GroundPoint(0f, 25f),
            new GroundPoint(0f, -25f),
            new GroundPoint(18f, 0f),
        };

        [Test]
        public void PicksSpawnFarthestFromNearestEnemy()
        {
            var enemies = new[] { new GroundPoint(0f, 20f), new GroundPoint(15f, 2f) };
            Assert.AreEqual(1, SpawnSelector.Pick(Spawns, enemies, new Random(1)));
        }

        [Test]
        public void NoEnemiesPicksAnySpawn()
        {
            var random = new Random(7);
            var seen = new bool[Spawns.Length];
            for (int i = 0; i < 200; i++) seen[SpawnSelector.Pick(Spawns, Array.Empty<GroundPoint>(), random)] = true;
            CollectionAssert.AreEqual(new[] { true, true, true }, seen);
        }

        [Test]
        public void NoSpawnsReturnsMinusOne()
        {
            Assert.AreEqual(-1, SpawnSelector.Pick(Array.Empty<GroundPoint>(), null, new Random(1)));
        }
    }
}
