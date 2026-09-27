using NUnit.Framework;

namespace ArenaShooter.Core.Tests
{
    public class MatchScoreTests
    {
        [Test]
        public void KillsScoreAndDeathsCount()
        {
            var score = new MatchScore(25);
            score.Register(1, "Player");
            score.Register(2, "Bot");
            score.RecordDeath(victimId: 2, killerId: 1);

            Assert.AreEqual(1, score.Get(1).Score);
            Assert.AreEqual(1, score.Get(2).Deaths);
            Assert.AreEqual(0, score.Get(2).Score);
        }

        [Test]
        public void SuicideCostsAPoint()
        {
            var score = new MatchScore(25);
            score.Register(1, "Player");
            score.RecordDeath(1, null);
            score.RecordDeath(1, 1);
            Assert.AreEqual(-2, score.Get(1).Score);
            Assert.AreEqual(2, score.Get(1).Deaths);
        }

        [Test]
        public void ReachingLimitWinsAndFreezesScore()
        {
            var score = new MatchScore(2);
            score.Register(1, "A");
            score.Register(2, "B");
            score.RecordDeath(2, 1);
            Assert.IsFalse(score.IsOver);
            score.RecordDeath(2, 1);
            Assert.IsTrue(score.IsOver);
            Assert.AreEqual(1, score.Winner.Id);

            score.RecordDeath(1, 2);
            Assert.AreEqual(0, score.Get(2).Kills, "No scoring after the match ends.");
        }

        [Test]
        public void RankedOrdersByScoreThenKillsThenDeaths()
        {
            var score = new MatchScore(25);
            score.Register(1, "A");
            score.Register(2, "B");
            score.Register(3, "C");
            score.RecordDeath(3, 2);
            score.RecordDeath(3, 2);
            score.RecordDeath(2, 1);
            score.RecordDeath(1, 3);

            var ranked = score.Ranked();
            Assert.AreEqual(new[] { 2, 1, 3 }, new[] { ranked[0].Id, ranked[1].Id, ranked[2].Id });
        }

        [Test]
        public void ResetClearsScores()
        {
            var score = new MatchScore(1);
            score.Register(1, "A");
            score.Register(2, "B");
            score.RecordDeath(2, 1);
            score.Reset();
            Assert.IsFalse(score.IsOver);
            Assert.AreEqual(0, score.Get(1).Kills);
        }
    }
}
