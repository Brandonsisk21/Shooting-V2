using ArenaShooter.Core;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>Assembles a bot: same body, motor, health and weapons as the player, plus a <see cref="BotController"/>.</summary>
    public static class BotFactory
    {
        public static readonly string[] Names = { "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf" };

        public static readonly Color[] Colors =
        {
            new Color(0.9f, 0.25f, 0.2f),  // red
            new Color(0.25f, 0.45f, 0.95f), // blue
            new Color(0.95f, 0.8f, 0.15f),  // yellow
            new Color(0.65f, 0.3f, 0.9f),   // purple
            new Color(0.15f, 0.85f, 0.85f), // cyan
            new Color(0.98f, 0.55f, 0.1f),  // orange
            new Color(0.95f, 0.4f, 0.7f),   // pink
        };

        public static BotSkill SkillFor(Difficulty difficulty) => difficulty switch
        {
            Difficulty.Easy => BotSkill.Easy(),
            Difficulty.Hard => BotSkill.Hard(),
            _ => BotSkill.Normal(),
        };

        public static Combatant Create(int index, Difficulty difficulty, Bounds roamArea, Transform parent)
        {
            string name = Names[index % Names.Length];
            Color color = Colors[index % Colors.Length];

            var root = new GameObject("Bot_" + name);
            root.transform.SetParent(parent, false);

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.4f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.05f;

            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0f, 1.6f, 0f);

            root.AddComponent<PlayerMotor>();
            root.AddComponent<Health>();

            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.minDistance = 4f;
            audio.maxDistance = 90f;

            var combatant = root.AddComponent<Combatant>();
            combatant.displayName = name;
            combatant.color = color;
            combatant.Eyes = head;

            var weapons = root.AddComponent<WeaponHolder>();
            weapons.aim = head;
            weapons.audioSource = audio;
            weapons.worldMuzzle = CombatantBody.Build(combatant, head, color, visible: true);

            var bot = root.AddComponent<BotController>();
            bot.head = head;
            bot.skill = SkillFor(difficulty);
            bot.roamArea = roamArea;
            return combatant;
        }
    }
}
