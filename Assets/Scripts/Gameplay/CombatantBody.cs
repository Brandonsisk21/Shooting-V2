using ArenaShooter.Core;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Builds a player-sized (1.8 m) body: trigger hitboxes for head and body, plus an optional
    /// gray-box visual (capsule, head, visor showing facing, held gun). Hitboxes are triggers so
    /// they never block movement; shots query triggers explicitly.
    /// </summary>
    public static class CombatantBody
    {
        public const float BodyHeight = 1.3f;
        public const float BodyRadius = 0.33f;
        public const float HeadRadius = 0.25f;
        public const float HeadY = 1.55f;

        /// <param name="head">Pivot that turns with aim (bots). The head hitbox and visor attach to it.</param>
        /// <returns>The gun's muzzle transform when visible, else null.</returns>
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

            GrayBox.Visual(PrimitiveType.Capsule, "Body", body.transform, Vector3.zero,
                new Vector3(BodyRadius * 2f, BodyHeight / 2f, BodyRadius * 2f), color);
            GrayBox.Visual(PrimitiveType.Cube, "Shoulders", body.transform, new Vector3(0f, 0.38f, 0f),
                new Vector3(0.78f, 0.22f, 0.4f), color * 0.85f);
            GrayBox.Visual(PrimitiveType.Sphere, "Head", headBox.transform, Vector3.zero, Vector3.one * HeadRadius * 2f, Color.Lerp(color, Color.white, 0.35f));

            // Visor and gun follow the aim pivot so you can read where a bot is looking.
            GrayBox.Visual(PrimitiveType.Cube, "Visor", head, new Vector3(0f, HeadY - head.localPosition.y + 0.03f, 0.22f),
                new Vector3(0.34f, 0.12f, 0.1f), new Color(0.95f, 0.8f, 0.3f));
            var gun = GrayBox.Visual(PrimitiveType.Cube, "Gun", head, new Vector3(0.28f, -0.3f, 0.35f), new Vector3(0.1f, 0.12f, 0.7f), new Color(0.2f, 0.22f, 0.25f));
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(gun.transform.parent, false);
            muzzle.localPosition = gun.transform.localPosition + new Vector3(0f, 0.02f, 0.37f);
            return muzzle;
        }
    }
}
