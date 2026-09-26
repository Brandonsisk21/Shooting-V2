using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>Saturated, cartoony colors for the outdoor gray-box pass (GDD 5).</summary>
    public static class OutdoorPalette
    {
        public static readonly Color Grass = new Color(0.44f, 0.7f, 0.33f);
        public static readonly Color Sand = new Color(0.9f, 0.8f, 0.56f);
        public static readonly Color Stone = new Color(0.8f, 0.76f, 0.68f);
        public static readonly Color StoneDark = new Color(0.62f, 0.58f, 0.53f);
        public static readonly Color Rock = new Color(0.56f, 0.51f, 0.47f);
        public static readonly Color RockLight = new Color(0.66f, 0.61f, 0.55f);
        public static readonly Color Cliff = new Color(0.5f, 0.45f, 0.41f);
        public static readonly Color CliffLight = new Color(0.6f, 0.54f, 0.48f);
        public static readonly Color Hills = new Color(0.33f, 0.5f, 0.33f);
        public static readonly Color Trunk = new Color(0.47f, 0.31f, 0.18f);
        public static readonly Color Leaves = new Color(0.27f, 0.6f, 0.25f);
        public static readonly Color LeavesLight = new Color(0.37f, 0.7f, 0.3f);
        public static readonly Color SniperPad = new Color(0.98f, 0.45f, 0.15f);
        /// <summary>North ("Red") base accents, for callouts now and team modes later.</summary>
        public static readonly Color RedAccent = new Color(0.88f, 0.3f, 0.26f);
        /// <summary>South ("Blue") base accents.</summary>
        public static readonly Color BlueAccent = new Color(0.25f, 0.47f, 0.88f);
    }
}
