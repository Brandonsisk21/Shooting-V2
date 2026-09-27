using NUnit.Framework;

namespace ArenaShooter.Core.Tests
{
    public class TileableNoiseTests
    {
        [Test]
        public void EdgesMatchSoTexturesTile()
        {
            for (float t = 0f; t < 1f; t += 0.037f)
            {
                Assert.AreEqual(TileableNoise.Fbm(0f, t, 4, 5, 7), TileableNoise.Fbm(1f, t, 4, 5, 7), 1e-5f, "left/right");
                Assert.AreEqual(TileableNoise.Fbm(t, 0f, 4, 5, 7), TileableNoise.Fbm(t, 1f, 4, 5, 7), 1e-5f, "top/bottom");
            }
        }

        [Test]
        public void StaysInZeroToOne()
        {
            for (float u = 0f; u < 1f; u += 0.05f)
            for (float v = 0f; v < 1f; v += 0.05f)
            {
                float n = TileableNoise.Fbm(u, v, 3, 4, 1);
                float r = TileableNoise.Ridged(u, v, 3, 4, 1);
                Assert.That(n, Is.InRange(0f, 1f));
                Assert.That(r, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void SeedsDiffer()
        {
            Assert.AreNotEqual(TileableNoise.Value(0.3f, 0.7f, 8, 1), TileableNoise.Value(0.3f, 0.7f, 8, 2));
        }
    }

    public class SurfacePatternTests
    {
        [TestCase(SurfaceKind.Ground)]
        [TestCase(SurfaceKind.Rock)]
        [TestCase(SurfaceKind.Plating)]
        public void PatternsAreValid(SurfaceKind kind)
        {
            var p = SurfacePatterns.Generate(kind, 64);
            Assert.AreEqual(64 * 64, p.Albedo.Length);
            Assert.AreEqual(64 * 64 * 3, p.Normal.Length);
            foreach (float a in p.Albedo) Assert.That(a, Is.InRange(0.3f, 1.3f));
            for (int i = 0; i < p.Normal.Length; i += 3)
            {
                float len = (float)System.Math.Sqrt(p.Normal[i] * p.Normal[i] + p.Normal[i + 1] * p.Normal[i + 1] + p.Normal[i + 2] * p.Normal[i + 2]);
                Assert.AreEqual(1f, len, 1e-3f);
                Assert.Greater(p.Normal[i + 2], 0f, "normals point out of the surface");
            }
        }
    }
}
