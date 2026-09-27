using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Places every piece twice: once as given and once rotated 180° about the map center (point
    /// symmetry, like Midship). Building through this guarantees neither half has an advantage.
    /// </summary>
    public sealed class SymmetricBuilder
    {
        public readonly Transform Root;

        public SymmetricBuilder(Transform root)
        {
            Root = root;
        }

        public static Vector3 Twin(Vector3 p) => new Vector3(-p.x, p.y, -p.z);

        public void Box(string name, Vector3 center, Vector3 size, Color color, float yaw = 0f) =>
            Box(name, center, size, color, color, yaw);

        /// <summary>A box pair where each half gets its own color (e.g. red/blue base accents).</summary>
        public void Box(string name, Vector3 center, Vector3 size, Color colorA, Color colorB, float yaw = 0f)
        {
            GrayBox.Box(name + "_A", center, size, colorA, Root, Quaternion.Euler(0f, yaw, 0f));
            GrayBox.Box(name + "_B", Twin(center), size, colorB, Root, Quaternion.Euler(0f, yaw + 180f, 0f));
        }

        public void Ramp(string name, Vector3 low, Vector3 high, float width, Color color)
        {
            GrayBox.Ramp(name + "_A", low, high, width, color, Root);
            GrayBox.Ramp(name + "_B", Twin(low), Twin(high), width, color, Root);
        }

        /// <summary>
        /// A giant alien mushroom: solid stalk, squashed pink cap with spots and glowing gills.
        /// The cap blocks shots/movement via an invisible box that matches its shape.
        /// </summary>
        public void Tree(string name, Vector3 basePosition, float height, float canopyRadius)
        {
            BuildMushroom(name + "_A", basePosition, height, canopyRadius);
            BuildMushroom(name + "_B", Twin(basePosition), height, canopyRadius);
        }

        /// <summary>A decoration pair with no collision (outside the play space or flush on surfaces).</summary>
        public GameObject[] Visual(PrimitiveType type, string name, Vector3 center, Vector3 scale, Color colorA, Color colorB, Vector3 euler = default, bool glow = false)
        {
            var rotA = Quaternion.Euler(euler);
            var rotB = Quaternion.Euler(0f, 180f, 0f) * rotA;
            var a = glow ? GrayBox.GlowVisual(type, name + "_A", Root, center, scale, colorA) : GrayBox.Visual(type, name + "_A", Root, center, scale, colorA);
            var b = glow ? GrayBox.GlowVisual(type, name + "_B", Root, Twin(center), scale, colorB) : GrayBox.Visual(type, name + "_B", Root, Twin(center), scale, colorB);
            a.transform.rotation = rotA;
            b.transform.rotation = rotB;
            return new[] { a, b };
        }

        public GameObject[] Visual(PrimitiveType type, string name, Vector3 center, Vector3 scale, Color color, Vector3 euler = default, bool glow = false) =>
            Visual(type, name, center, scale, color, color, euler, glow);

        /// <summary>A chunky boulder: a large box with a smaller offset box on top.</summary>
        public void Rock(string name, Vector3 basePosition, Vector3 size, float yaw)
        {
            BuildRock(name + "_A", basePosition, size, yaw);
            BuildRock(name + "_B", Twin(basePosition), size, yaw + 180f);
        }

        public SpawnPoint[] Spawn(string name, Vector3 feetPosition)
        {
            return new[]
            {
                SpawnPoint.Create(name + "_A", feetPosition, Vector3.zero, Root),
                SpawnPoint.Create(name + "_B", Twin(feetPosition), Vector3.zero, Root),
            };
        }

        private void BuildMushroom(string name, Vector3 basePosition, float height, float capRadius)
        {
            var tree = new GameObject(name).transform;
            tree.SetParent(Root, false);
            tree.position = basePosition;

            const float stalkRadius = 0.35f;
            GrayBox.Solid(PrimitiveType.Cylinder, "Stalk", tree, basePosition + Vector3.up * (height * 0.5f),
                new Vector3(stalkRadius * 2f, height * 0.5f, stalkRadius * 2f), OutdoorPalette.Trunk);

            // Visual() takes positions local to the mushroom; Solid()/Blocker() take world positions.
            Vector3 cap = Vector3.up * (height + capRadius * 0.3f);
            GrayBox.Visual(PrimitiveType.Sphere, "Cap", tree, cap, new Vector3(capRadius * 2f, capRadius * 0.85f, capRadius * 2f), OutdoorPalette.Leaves);
            GrayBox.Blocker("CapCollider", basePosition + cap, new Vector3(capRadius * 1.5f, capRadius * 0.7f, capRadius * 1.5f), tree);

            // White spots on top and a ring of glowing gills underneath.
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f * Mathf.Deg2Rad;
                var spot = cap + new Vector3(Mathf.Cos(a) * capRadius * 0.55f, capRadius * 0.34f, Mathf.Sin(a) * capRadius * 0.55f);
                GrayBox.Visual(PrimitiveType.Sphere, "Spot", tree, spot, new Vector3(0.45f, 0.15f, 0.45f) * capRadius * 0.6f, OutdoorPalette.LeavesLight);
                var gill = cap + new Vector3(Mathf.Cos(a + 0.6f) * capRadius * 0.6f, -capRadius * 0.3f, Mathf.Sin(a + 0.6f) * capRadius * 0.6f);
                GrayBox.GlowVisual(PrimitiveType.Sphere, "Glow", tree, gill, Vector3.one * 0.18f, OutdoorPalette.Glow);
            }
        }

        private void BuildRock(string name, Vector3 basePosition, Vector3 size, float yaw)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var rock = new GameObject(name).transform;
            rock.SetParent(Root, false);
            rock.position = basePosition;
            GrayBox.Box("Base", basePosition + Vector3.up * (size.y * 0.5f), size, OutdoorPalette.Rock, rock, rotation);
            Vector3 topSize = new Vector3(size.x * 0.6f, size.y * 0.35f, size.z * 0.6f);
            Vector3 topCenter = basePosition + rotation * new Vector3(size.x * 0.12f, size.y + topSize.y * 0.5f, -size.z * 0.1f);
            GrayBox.Box("Top", topCenter, topSize, OutdoorPalette.RockLight, rock, rotation * Quaternion.Euler(0f, 17f, 0f));
        }
    }
}
