using System;
using System.IO;
using NUnit.Framework;

namespace ArenaShooter.Core.Tests
{
    /// <summary>Checks the real generated model files (Assets/Resources/Models) load and are sane.</summary>
    public class ModelFormatTests
    {
        private static readonly string[] AllModels =
            { "grunt", "pew_rifle", "long_zapper", "mushroom", "dropship", "crate", "rock_a", "rock_b", "rock_c", "boom_bomb" };

        private static string ModelsDir()
        {
            foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    var candidate = Path.Combine(dir.FullName, "Assets", "Resources", "Models");
                    if (Directory.Exists(candidate)) return candidate;
                    dir = dir.Parent;
                }
            }
            Assert.Fail("Could not find Assets/Resources/Models");
            return null;
        }

        private static ModelData Load(string name) => ModelFormat.Parse(File.ReadAllBytes(Path.Combine(ModelsDir(), name + ".bytes")));

        [TestCaseSource(nameof(AllModels))]
        public void ModelLoadsWithValidGeometry(string name)
        {
            var model = Load(name);
            Assert.IsNotEmpty(model.Parts);
            foreach (var part in model.Parts)
            {
                Assert.AreEqual(part.Positions.Length, part.Normals.Length, part.Name);
                Assert.Greater(part.VertexCount, 0, part.Name);
                foreach (var sub in part.Submeshes)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(sub.Material), part.Name);
                    Assert.AreEqual(0, sub.Indices.Length % 3, part.Name);
                }
                for (int i = 0; i < part.Normals.Length; i += 3)
                {
                    float len = (float)Math.Sqrt(part.Normals[i] * part.Normals[i] + part.Normals[i + 1] * part.Normals[i + 1] + part.Normals[i + 2] * part.Normals[i + 2]);
                    Assert.AreEqual(1f, len, 0.02f, $"{name}/{part.Name} normal {i / 3}");
                }
            }
        }

        /// <summary>
        /// Triangle winding must agree with the vertex normals (front faces point outward),
        /// otherwise Unity's back-face culling would show models inside out.
        /// </summary>
        [TestCaseSource(nameof(AllModels))]
        public void TrianglesFaceOutward(string name)
        {
            int agree = 0, total = 0;
            foreach (var part in Load(name).Parts)
                foreach (var sub in part.Submeshes)
                    for (int t = 0; t < sub.Indices.Length; t += 3)
                    {
                        int a = sub.Indices[t] * 3, b = sub.Indices[t + 1] * 3, c = sub.Indices[t + 2] * 3;
                        var p = part.Positions;
                        float ux = p[b] - p[a], uy = p[b + 1] - p[a + 1], uz = p[b + 2] - p[a + 2];
                        float vx = p[c] - p[a], vy = p[c + 1] - p[a + 1], vz = p[c + 2] - p[a + 2];
                        float fx = uy * vz - uz * vy, fy = uz * vx - ux * vz, fz = ux * vy - uy * vx;
                        if (fx * fx + fy * fy + fz * fz < 1e-14f) continue; // degenerate (poles)
                        var n = part.Normals;
                        float nx = n[a] + n[b] + n[c], ny = n[a + 1] + n[b + 1] + n[c + 1], nz = n[a + 2] + n[b + 2] + n[c + 2];
                        total++;
                        if (fx * nx + fy * ny + fz * nz > 0f) agree++;
                    }
            Assert.Greater(total, 0);
            Assert.GreaterOrEqual(agree / (float)total, 0.98f, $"{name}: {agree}/{total} faces agree with normals");
        }

        [Test]
        public void GruntMatchesGameplaySizeAndHasAnimatableParts()
        {
            var grunt = Load("grunt");
            foreach (var part in new[] { "Torso", "LegL", "LegR", "Arms", "Head", "Helmet" })
                Assert.IsNotNull(grunt.Part(part), part);
            Assert.IsTrue(grunt.Markers.ContainsKey("gunMount"));

            grunt.GetBounds(out var min, out var max);
            Assert.AreEqual(0f, min[1], 0.02f, "feet on the ground");
            Assert.AreEqual(1.85f, max[1], 0.06f, "player height ~1.8 m (+ helmet)");
        }

        [TestCase("pew_rifle")]
        [TestCase("long_zapper")]
        public void WeaponsHaveMuzzleInFront(string name)
        {
            var model = Load(name);
            Assert.IsTrue(model.Markers.TryGetValue("muzzle", out var muzzle));
            model.GetBounds(out _, out var max);
            Assert.Greater(muzzle[2], max[2] - 0.05f, "muzzle at the front of the barrel");
        }
    }
}
