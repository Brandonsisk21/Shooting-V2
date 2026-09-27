using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// "Space Grunts" alien-planet palette (GDD 5.1): pastel, slightly muted world colors so the
    /// brightly colored grunts pop against it.
    /// </summary>
    public static class OutdoorPalette
    {
        public static readonly Color Grass = new Color(0.56f, 0.47f, 0.78f);      // lilac alien turf
        public static readonly Color Sand = new Color(0.97f, 0.76f, 0.58f);       // peach dust path
        public static readonly Color Stone = new Color(0.88f, 0.86f, 0.8f);       // cream hull plating
        public static readonly Color StoneDark = new Color(0.52f, 0.56f, 0.66f);  // gunmetal
        public static readonly Color Rock = new Color(0.43f, 0.4f, 0.62f);        // dusky violet rock
        public static readonly Color RockLight = new Color(0.54f, 0.5f, 0.74f);
        public static readonly Color Cliff = new Color(0.37f, 0.32f, 0.54f);
        public static readonly Color CliffLight = new Color(0.46f, 0.4f, 0.64f);
        public static readonly Color Hills = new Color(0.32f, 0.26f, 0.47f);
        public static readonly Color Trunk = new Color(0.96f, 0.91f, 0.78f);      // mushroom stalk
        public static readonly Color Leaves = new Color(0.98f, 0.44f, 0.6f);      // mushroom cap pink
        public static readonly Color LeavesLight = new Color(1f, 0.97f, 0.92f);   // cap spots
        public static readonly Color Glow = new Color(0.45f, 0.98f, 0.9f);        // bioluminescent teal
        public static readonly Color Crate = new Color(0.95f, 0.62f, 0.2f);       // supply crates
        public static readonly Color Window = new Color(0.5f, 0.92f, 1f);
        public static readonly Color EngineGlow = new Color(1f, 0.6f, 0.25f);
        public static readonly Color SniperPad = new Color(0.98f, 0.45f, 0.15f);
        /// <summary>North ("Red") base accents, for callouts now and team modes later.</summary>
        public static readonly Color RedAccent = new Color(0.92f, 0.28f, 0.3f);
        /// <summary>South ("Blue") base accents.</summary>
        public static readonly Color BlueAccent = new Color(0.25f, 0.5f, 0.95f);
    }
}
