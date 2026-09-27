using System.Collections.Generic;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>What a map builder hands back to the bootstrap.</summary>
    public sealed class MapInfo
    {
        public string Name;
        public Transform Root;
        public SniperSpawnPad SniperPad;
        public readonly List<SpawnPoint> Spawns = new List<SpawnPoint>();
        /// <summary>Playable volume: bots roam inside it and the nav mesh is baked from it.</summary>
        public Bounds PlayArea;
        /// <summary>Whether the match fills this map with bots.</summary>
        public bool HasBots;
    }
}
