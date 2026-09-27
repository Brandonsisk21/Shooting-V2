using System;

namespace ArenaShooter.Core
{
    public enum MapChoice
    {
        CrashSite,
        TestRange,
    }

    public enum Difficulty
    {
        Easy,
        Normal,
        Hard,
    }

    /// <summary>What the player picks on the match setup screen (GDD 4.1 / 4.2 defaults).</summary>
    [Serializable]
    public class MatchSetup
    {
        public MapChoice map = MapChoice.CrashSite;
        /// <summary>Bots besides you. 5 + you = 6 players, inside the GDD's 4–8.</summary>
        public int botCount = 5;
        public Difficulty difficulty = Difficulty.Normal;
        public int scoreLimit = 25;
        /// <summary>Match length in minutes; 0 = no time limit.</summary>
        public int timeLimitMinutes = 10;

        public static readonly int[] ScoreLimitChoices = { 10, 15, 25, 50 };
        public static readonly int[] TimeLimitChoices = { 5, 10, 15, 20, 0 };
        public const int MaxBots = 7;

        public float TimeLimitSeconds => timeLimitMinutes * 60f;
        public bool HasTimeLimit => timeLimitMinutes > 0;

        public MatchSetup Clone() => (MatchSetup)MemberwiseClone();
    }
}
