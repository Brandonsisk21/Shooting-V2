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

        /// <summary>A cartoony tree: cylinder trunk plus two overlapping sphere canopies. All solid.</summary>
        public void Tree(string name, Vector3 basePosition, float height, float canopyRadius)
        {
            BuildTree(name + "_A", basePosition, height, canopyRadius);
            BuildTree(name + "_B", Twin(basePosition), height, canopyRadius);
        }

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

        private void BuildTree(string name, Vector3 basePosition, float height, float canopyRadius)
        {
            var tree = new GameObject(name).transform;
            tree.SetParent(Root, false);
            tree.position = basePosition;

            const float trunkRadius = 0.3f;
            GrayBox.Solid(PrimitiveType.Cylinder, "Trunk", tree, basePosition + Vector3.up * (height * 0.5f),
                new Vector3(trunkRadius * 2f, height * 0.5f, trunkRadius * 2f), OutdoorPalette.Trunk);
            Vector3 canopy = basePosition + Vector3.up * (height + canopyRadius * 0.5f);
            GrayBox.Solid(PrimitiveType.Sphere, "Canopy", tree, canopy, Vector3.one * canopyRadius * 2f, OutdoorPalette.Leaves);
            GrayBox.Solid(PrimitiveType.Sphere, "CanopyTop", tree, canopy + new Vector3(0.4f, canopyRadius * 0.7f, -0.3f),
                Vector3.one * canopyRadius * 1.3f, OutdoorPalette.LeavesLight);
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
