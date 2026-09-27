using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Goofy toy-blaster weapon models (GDD 5.1), built from primitives so the same model serves
    /// the first-person view, pickups and bots' hands. Local space: +z forward, origin at the grip.
    /// </summary>
    public static class WeaponModels
    {
        private static readonly Color Toy = new Color(0.96f, 0.95f, 0.9f);
        private static readonly Color Orange = new Color(0.99f, 0.56f, 0.2f);
        private static readonly Color Metal = new Color(0.36f, 0.38f, 0.46f);
        private static readonly Color Zapper = new Color(0.5f, 0.42f, 0.78f);
        public static readonly Color PewGlow = new Color(1f, 0.55f, 0.25f);
        public static readonly Color ZapGlow = new Color(0.45f, 0.95f, 1f);

        /// <summary>Builds the model under <paramref name="parent"/> and returns its muzzle point.</summary>
        public static Transform Build(string weaponId, Transform parent, float scale = 1f)
        {
            var root = new GameObject("Model_" + weaponId).transform;
            root.SetParent(parent, false);
            root.localScale = Vector3.one * scale;
            Transform muzzle = weaponId == "sniper" ? BuildLongZapper(root) : BuildPewRifle(root);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return muzzle;
        }

        /// <summary>Chunky rounded blaster with an energy cell, a stubby barrel and a silly antenna.</summary>
        private static Transform BuildPewRifle(Transform root)
        {
            Part(PrimitiveType.Capsule, "Body", root, new Vector3(0f, 0f, 0.26f), new Vector3(0.14f, 0.28f, 0.14f), Toy, new Vector3(90f, 0f, 0f));
            Part(PrimitiveType.Cube, "TopRail", root, new Vector3(0f, 0.075f, 0.22f), new Vector3(0.09f, 0.05f, 0.34f), Orange);
            Part(PrimitiveType.Cube, "Grip", root, new Vector3(0f, -0.1f, 0.06f), new Vector3(0.06f, 0.15f, 0.07f), Metal, new Vector3(15f, 0f, 0f));
            Part(PrimitiveType.Cylinder, "Barrel", root, new Vector3(0f, 0f, 0.58f), new Vector3(0.07f, 0.09f, 0.07f), Metal, new Vector3(90f, 0f, 0f));
            Glow(PrimitiveType.Cylinder, "MuzzleRing", root, new Vector3(0f, 0f, 0.67f), new Vector3(0.1f, 0.012f, 0.1f), PewGlow, new Vector3(90f, 0f, 0f));
            Glow(PrimitiveType.Cylinder, "EnergyCell", root, new Vector3(0.075f, -0.01f, 0.2f), new Vector3(0.07f, 0.06f, 0.07f), PewGlow, new Vector3(0f, 0f, 90f));
            Part(PrimitiveType.Cylinder, "Antenna", root, new Vector3(-0.035f, 0.2f, 0.1f), new Vector3(0.012f, 0.12f, 0.012f), Metal);
            Glow(PrimitiveType.Sphere, "AntennaBall", root, new Vector3(-0.035f, 0.33f, 0.1f), Vector3.one * 0.05f, new Color(1f, 0.25f, 0.3f));
            return Muzzle(root, 0.69f);
        }

        /// <summary>Absurdly long barrel with glowing coils and a satellite-dish scope.</summary>
        private static Transform BuildLongZapper(Transform root)
        {
            Part(PrimitiveType.Cube, "Body", root, new Vector3(0f, 0f, 0.15f), new Vector3(0.1f, 0.13f, 0.46f), Zapper);
            Part(PrimitiveType.Cube, "Stock", root, new Vector3(0f, -0.02f, -0.16f), new Vector3(0.08f, 0.12f, 0.2f), Metal);
            Part(PrimitiveType.Cube, "Grip", root, new Vector3(0f, -0.11f, 0.04f), new Vector3(0.06f, 0.15f, 0.07f), Metal, new Vector3(15f, 0f, 0f));
            Part(PrimitiveType.Cylinder, "Barrel", root, new Vector3(0f, 0.01f, 0.82f), new Vector3(0.05f, 0.45f, 0.05f), Toy, new Vector3(90f, 0f, 0f));
            foreach (float z in new[] { 0.55f, 0.78f, 1.01f })
                Glow(PrimitiveType.Cylinder, "Coil", root, new Vector3(0f, 0.01f, z), new Vector3(0.09f, 0.012f, 0.09f), ZapGlow, new Vector3(90f, 0f, 0f));
            Part(PrimitiveType.Sphere, "Dish", root, new Vector3(0f, 0.15f, 0.12f), new Vector3(0.22f, 0.05f, 0.22f), new Color(0.85f, 0.87f, 0.92f), new Vector3(-70f, 0f, 0f));
            Part(PrimitiveType.Cylinder, "DishStem", root, new Vector3(0f, 0.1f, 0.12f), new Vector3(0.02f, 0.04f, 0.02f), Metal);
            Glow(PrimitiveType.Sphere, "DishTip", root, new Vector3(0f, 0.19f, 0.16f), Vector3.one * 0.035f, ZapGlow);
            return Muzzle(root, 1.29f);
        }

        private static void Part(PrimitiveType type, string name, Transform root, Vector3 pos, Vector3 scale, Color color, Vector3 euler = default)
        {
            GrayBox.Visual(type, name, root, pos, scale, color).transform.localRotation = Quaternion.Euler(euler);
        }

        private static void Glow(PrimitiveType type, string name, Transform root, Vector3 pos, Vector3 scale, Color color, Vector3 euler = default)
        {
            GrayBox.GlowVisual(type, name, root, pos, scale, color).transform.localRotation = Quaternion.Euler(euler);
        }

        private static Transform Muzzle(Transform root, float z)
        {
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root, false);
            muzzle.localPosition = new Vector3(0f, 0.01f, z);
            return muzzle;
        }
    }
}
