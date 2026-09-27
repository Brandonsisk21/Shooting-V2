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

        public SpawnPoint[] Spawn(string name, Vector3 feetPosition)
        {
            return new[]
            {
                SpawnPoint.Create(name + "_A", feetPosition, Vector3.zero, Root),
                SpawnPoint.Create(name + "_B", Twin(feetPosition), Vector3.zero, Root),
            };
        }

        /// <summary>
        /// A rock-shaped obstacle: the box stays as the (invisible) collider, dressed with a
        /// lumpy 3D rock stretched over it. Falls back to the visible box without models.
        /// </summary>
        public void RockBox(string name, Vector3 center, Vector3 size, Color color, float yaw = 0f)
        {
            BuildRockBox(name + "_A", center, size, color, yaw);
            BuildRockBox(name + "_B", Twin(center), size, color, yaw + 180f);
        }

        /// <summary>A supply crate obstacle: invisible box collider + crate model.</summary>
        public void Crate(string name, Vector3 center, float size, float yaw)
        {
            foreach (var (pos, rot) in new[] { (center, yaw), (Twin(center), yaw + 180f) })
            {
                var box = GrayBox.Box(name, pos, Vector3.one * size, OutdoorPalette.Crate, Root, Quaternion.Euler(0f, rot, 0f));
                if (!ModelLibrary.Has("crate")) continue;
                box.GetComponent<Renderer>().enabled = false;
                ModelLibrary.SpawnFitted("crate", box.transform.parent, pos, Vector3.one * size, Quaternion.Euler(0f, rot, 0f), Color.white, inflate: 1.02f);
            }
        }

        /// <summary>A 3D model pair with team colors per half (e.g. the crashed dropships).</summary>
        public void ModelPair(string model, Vector3 position, Vector3 euler, float scale, Color teamA, Color teamB)
        {
            foreach (var (pos, rot, team) in new[]
                     {
                         (position, Quaternion.Euler(euler), teamA),
                         (Twin(position), Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(euler), teamB),
                     })
            {
                var holder = new GameObject(model).transform;
                holder.SetParent(Root, false);
                holder.SetPositionAndRotation(pos, rot);
                holder.localScale = Vector3.one * scale;
                ModelLibrary.Spawn(model, holder, team);
            }
        }

        private void BuildRockBox(string name, Vector3 center, Vector3 size, Color color, float yaw)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var box = GrayBox.Box(name, center, size, color, Root, rotation);
            if (!ModelLibrary.Has("rock_a")) return;
            box.GetComponent<Renderer>().enabled = false;
            // Pick a rock shape from the position (stable between runs, varied across the map).
            string variant = "rock_" + "abc"[Mathf.Abs(Mathf.RoundToInt(center.x * 7f + center.z * 13f + center.y * 3f)) % 3];
            ModelLibrary.SpawnFitted(variant, Root, center, size, rotation, Color.white, color, inflate: 1.12f).name = name + "_Rock";
        }

        private void BuildMushroom(string name, Vector3 basePosition, float height, float capRadius)
        {
            var tree = new GameObject(name).transform;
            tree.SetParent(Root, false);
            tree.position = basePosition;

            if (ModelLibrary.Has("mushroom"))
            {
                // 3D mushroom scaled so its stalk top matches `height`; colliders sized to match.
                const float modelStalkTop = 3.9f, modelCapRadius = 1.7f, modelCapCenter = 4.4f;
                float s = height / modelStalkTop;
                var holder = new GameObject("Model").transform;
                holder.SetParent(tree, false);
                holder.localScale = Vector3.one * s;
                ModelLibrary.Spawn("mushroom", holder, Color.white);
                var stalk = GrayBox.Solid(PrimitiveType.Cylinder, "StalkCollider", tree, basePosition + Vector3.up * (height * 0.5f),
                    new Vector3(0.8f * s, height * 0.5f, 0.8f * s), OutdoorPalette.Trunk);
                stalk.GetComponent<Renderer>().enabled = false;
                GrayBox.Blocker("CapCollider", basePosition + Vector3.up * (modelCapCenter * s),
                    new Vector3(modelCapRadius * 1.5f * s, 1.0f * s, modelCapRadius * 1.5f * s), tree);
                return;
            }

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

    }
}
