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
    }
}
