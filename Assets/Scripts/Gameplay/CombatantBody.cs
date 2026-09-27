using ArenaShooter.Core;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Builds a player-sized (1.8 m) combatant: trigger hitboxes for head and body (the gameplay
    /// shape, unchanged by art), plus an optional "Space Grunt" look (GDD 5.1): stubby legs, chunky
    /// armor in the player's color, backpack with antenna, a big goofy head in a fishbowl helmet.
    /// Hitboxes are triggers so they never block movement; shots query triggers explicitly.
    /// </summary>
    public static class CombatantBody
    {
        public const float BodyHeight = 1.3f;
        public const float BodyRadius = 0.33f;
        public const float HeadRadius = 0.25f;
        public const float HeadY = 1.55f;

        private static readonly Color Suit = new Color(0.26f, 0.28f, 0.36f);
        private static readonly Color Metal = new Color(0.55f, 0.58f, 0.66f);
        private static readonly Color Skin = new Color(1f, 0.82f, 0.64f);
        private static readonly Color HelmetGlass = new Color(0.75f, 0.95f, 1f, 0.28f);

        /// <param name="head">Pitch pivot that turns with aim. Bots hold their weapon from it.</param>
        /// <returns>Where the held weapon attaches (bots), or null when not visible.</returns>
        public static Transform Build(Combatant combatant, Transform head, Color color, bool visible)
        {
            Transform root = combatant.transform;

            var body = new GameObject("BodyHitbox");
            body.transform.SetParent(root, false);
            body.transform.localPosition = new Vector3(0f, BodyHeight / 2f, 0f);
            var capsule = body.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.height = BodyHeight;
            capsule.radius = BodyRadius;
            body.AddComponent<Hitbox>().zone = HitZone.Body;
            combatant.ChestCenter = body.transform;

            var headBox = new GameObject("HeadHitbox");
            headBox.transform.SetParent(root, false);
            headBox.transform.localPosition = new Vector3(0f, HeadY, 0f);
            var sphere = headBox.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = HeadRadius;
            headBox.AddComponent<Hitbox>().zone = HitZone.Head;
            combatant.HeadCenter = headBox.transform;

            if (!visible) return null;

            var visual = new GameObject("GruntVisual").transform;
            visual.SetParent(root, false);
            Color armor = color;
            Color armorDark = Color.Lerp(color, Color.black, 0.25f);

            // Legs and boots.
            foreach (float x in new[] { -0.13f, 0.13f })
            {
                GrayBox.Visual(PrimitiveType.Capsule, "Leg", visual, new Vector3(x, 0.24f, 0f), new Vector3(0.22f, 0.2f, 0.22f), Suit);
                GrayBox.Visual(PrimitiveType.Cube, "Boot", visual, new Vector3(x, 0.06f, 0.04f), new Vector3(0.2f, 0.12f, 0.3f), Metal);
            }

            // Chunky armored torso, belly plate, shoulder pads, arms.
            GrayBox.Visual(PrimitiveType.Capsule, "Torso", visual, new Vector3(0f, 0.8f, 0f), new Vector3(0.72f, 0.42f, 0.6f), armor);
            GrayBox.Visual(PrimitiveType.Sphere, "BellyPlate", visual, new Vector3(0f, 0.74f, 0.22f), new Vector3(0.46f, 0.42f, 0.2f), Color.Lerp(color, Color.white, 0.4f));
            foreach (float x in new[] { -0.37f, 0.37f })
            {
                GrayBox.Visual(PrimitiveType.Sphere, "ShoulderPad", visual, new Vector3(x, 1.02f, 0f), Vector3.one * 0.32f, armorDark);
                GrayBox.Visual(PrimitiveType.Capsule, "Arm", visual, new Vector3(x * 1.05f, 0.76f, 0.06f), new Vector3(0.16f, 0.2f, 0.16f), Suit);
            }

            // Backpack with a wobbly antenna and a glowing tip in the player's color.
            GrayBox.Visual(PrimitiveType.Cube, "Backpack", visual, new Vector3(0f, 0.86f, -0.33f), new Vector3(0.46f, 0.5f, 0.24f), Metal);
            GrayBox.Visual(PrimitiveType.Cylinder, "Antenna", visual, new Vector3(0.15f, 1.32f, -0.36f), new Vector3(0.025f, 0.25f, 0.025f), Metal);
            GrayBox.GlowVisual(PrimitiveType.Sphere, "AntennaTip", visual, new Vector3(0.15f, 1.59f, -0.36f), Vector3.one * 0.1f, color);

            // Big goofy head with cartoon eyes inside a fishbowl helmet (bobbles as they move).
            var headGroup = new GameObject("HeadGroup").transform;
            headGroup.SetParent(visual, false);
            headGroup.localPosition = new Vector3(0f, 1.27f, 0f);
            GrayBox.Visual(PrimitiveType.Cylinder, "HelmetRim", headGroup, Vector3.zero, new Vector3(0.58f, 0.03f, 0.58f), armor);
            GrayBox.Visual(PrimitiveType.Sphere, "Head", headGroup, new Vector3(0f, 0.24f, 0f), Vector3.one * 0.36f, Skin);
            foreach (float x in new[] { -0.075f, 0.075f })
            {
                GrayBox.Visual(PrimitiveType.Sphere, "Eye", headGroup, new Vector3(x, 0.28f, 0.14f), Vector3.one * 0.1f, Color.white);
                GrayBox.Visual(PrimitiveType.Sphere, "Pupil", headGroup, new Vector3(x * 1.1f, 0.28f, 0.185f), Vector3.one * 0.05f, new Color(0.08f, 0.08f, 0.12f));
            }
            var bubble = GrayBox.Visual(PrimitiveType.Sphere, "HelmetBubble", headGroup, new Vector3(0f, 0.25f, 0f), Vector3.one * 0.62f, HelmetGlass);
            bubble.GetComponent<Renderer>().sharedMaterial = GrayBox.Glass(HelmetGlass);

            var grunt = combatant.gameObject.AddComponent<GruntVisual>();
            grunt.visual = visual;
            grunt.headGroup = headGroup;
            grunt.color = color;

            var gunMount = new GameObject("GunMount").transform;
            gunMount.SetParent(head, false);
            gunMount.localPosition = new Vector3(0.26f, -0.3f, 0.12f);
            return gunMount;
        }
    }
}
