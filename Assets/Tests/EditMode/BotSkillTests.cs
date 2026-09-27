using NUnit.Framework;

namespace ArenaShooter.Core.Tests
{
    public class BotSkillTests
    {
        [Test]
        public void AimErrorShrinksWhileTracking()
        {
            var skill = BotSkill.Normal();
            Assert.AreEqual(skill.initialAimError, skill.AimErrorAfter(0f), 1e-4f);
            Assert.Less(skill.AimErrorAfter(skill.aimSettleTime * 0.5f), skill.initialAimError);
            Assert.AreEqual(skill.settledAimError, skill.AimErrorAfter(skill.aimSettleTime * 5f), 1e-4f);
        }

        [Test]
        public void VisionConeAndRange()
        {
            var skill = BotSkill.Normal(); // 140° cone, 70 m
            Assert.IsTrue(skill.CanSee(60f, 30f));
            Assert.IsFalse(skill.CanSee(80f, 30f));
            Assert.IsFalse(skill.CanSee(0f, 80f));
        }

        [Test]
        public void HarderPresetsAreBetterAcrossTheBoard()
        {
            var easy = BotSkill.Easy();
            var normal = BotSkill.Normal();
            var hard = BotSkill.Hard();
            Assert.Greater(easy.reactionTime, normal.reactionTime);
            Assert.Greater(normal.reactionTime, hard.reactionTime);
            Assert.Greater(easy.settledAimError, normal.settledAimError);
            Assert.Greater(normal.settledAimError, hard.settledAimError);
        }
    }
}
