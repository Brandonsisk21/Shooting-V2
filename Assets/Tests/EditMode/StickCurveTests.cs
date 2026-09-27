using NUnit.Framework;

namespace ArenaShooter.Core.Tests
{
    public class StickCurveTests
    {
        [Test]
        public void InsideDeadzoneIsZero()
        {
            StickCurve.Apply(0.1f, 0.05f, 0.15f, 2f, out float x, out float y);
            Assert.AreEqual(0f, x);
            Assert.AreEqual(0f, y);
        }

        [Test]
        public void FullTiltIsFullOutput()
        {
            StickCurve.Apply(1f, 0f, 0.15f, 2f, out float x, out float y);
            Assert.AreEqual(1f, x, 1e-5f);
            Assert.AreEqual(0f, y, 1e-5f);
        }

        [Test]
        public void CurveGivesFineControlNearCenter()
        {
            StickCurve.Apply(0.575f, 0f, 0.15f, 2f, out float x, out _);
            Assert.AreEqual(0.25f, x, 1e-4f); // halfway past the deadzone, squared
        }

        [Test]
        public void KeepsDirection()
        {
            StickCurve.Apply(0.6f, 0.8f, 0.15f, 2f, out float x, out float y);
            Assert.AreEqual(0.8f / 0.6f, y / x, 1e-4f);
        }
    }
}
