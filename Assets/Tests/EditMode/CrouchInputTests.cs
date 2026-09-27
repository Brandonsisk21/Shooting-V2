using NUnit.Framework;

namespace ArenaShooter.Core.Tests
{
    public class CrouchInputTests
    {
        [Test]
        public void HoldFollowsTheButton()
        {
            var c = new CrouchInput();
            Assert.IsTrue(c.Update(CrouchMode.Hold, held: true, pressed: true, jump: false));
            Assert.IsTrue(c.Update(CrouchMode.Hold, true, false, false));
            Assert.IsFalse(c.Update(CrouchMode.Hold, false, false, false));
        }

        [Test]
        public void ToggleFlipsOnEachPress()
        {
            var c = new CrouchInput();
            Assert.IsTrue(c.Update(CrouchMode.Toggle, true, true, false));
            Assert.IsTrue(c.Update(CrouchMode.Toggle, false, false, false), "stays down after letting go");
            Assert.IsFalse(c.Update(CrouchMode.Toggle, true, true, false));
            Assert.IsFalse(c.Update(CrouchMode.Toggle, false, false, false));
        }

        [Test]
        public void JumpingOrResetStandsUpInToggleMode()
        {
            var c = new CrouchInput();
            c.Update(CrouchMode.Toggle, true, true, false);
            Assert.IsFalse(c.Update(CrouchMode.Toggle, false, false, jump: true));
            c.Update(CrouchMode.Toggle, true, true, false);
            c.Reset();
            Assert.IsFalse(c.Update(CrouchMode.Toggle, false, false, false));
        }

        [Test]
        public void SwitchingToHoldClearsToggle()
        {
            var c = new CrouchInput();
            c.Update(CrouchMode.Toggle, true, true, false);
            Assert.IsFalse(c.Update(CrouchMode.Hold, false, false, false));
            Assert.IsFalse(c.Update(CrouchMode.Toggle, false, false, false));
        }
    }
}
