using ArenaShooter.Core;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>Assembles a bot: same body, motor, health and weapons as the player, plus a <see cref="BotController"/>.</summary>
    public static class BotFactory
    {
        /// <summary>Goofy Space Grunt call signs (GDD 5.1).</summary>
        public static readonly string[] Names = { "Pvt. Pickles", "Sgt. Noodle", "Cpl. Bonk", "Pvt. Zorp", "Lt. Wobbles", "Sgt. Muffin", "Pvt. Gary" };

        public static readonly Color[] Colors =
        {
            new Color(0.9f, 0.25f, 0.2f),  // red
            new Color(0.25f, 0.45f, 0.95f), // blue
            new Color(0.95f, 0.8f, 0.15f),  // yellow
            new Color(0.55f, 0.95f, 0.3f),  // lime (purple would blend into the lilac ground)
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
            var combatant = CombatantFactory.CreateBody(name, color, parent, networkDriven: false);
            combatant.gameObject.name = "Bot_" + name;

            var bot = combatant.gameObject.AddComponent<BotController>();
            bot.head = combatant.Eyes;
            bot.skill = SkillFor(difficulty);
            bot.roamArea = roamArea;
            return combatant;
        }
    }
}
