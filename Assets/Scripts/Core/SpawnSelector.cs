using System;
using System.Collections.Generic;

namespace ArenaShooter.Core
{
    /// <summary>A position on the ground plane (x, z) in meters.</summary>
    public readonly struct GroundPoint
    {
        public readonly float X;
        public readonly float Z;

        public GroundPoint(float x, float z)
        {
            X = x;
            Z = z;
        }

        public float DistanceSquared(GroundPoint other)
        {
            float dx = X - other.X, dz = Z - other.Z;
            return dx * dx + dz * dz;
        }
    }

    /// <summary>
    /// FFA spawn choice: pick the spawn whose nearest living enemy is farthest away. With no
    /// enemies (e.g. solo testing) every spawn scores the same and one is picked at random.
    /// </summary>
    public static class SpawnSelector
    {
        public static int Pick(IReadOnlyList<GroundPoint> spawns, IReadOnlyList<GroundPoint> enemies, Random random)
        {
            if (spawns == null || spawns.Count == 0) return -1;
            if (enemies == null || enemies.Count == 0) return random.Next(spawns.Count);

            int best = -1;
            float bestScore = float.NegativeInfinity;
            int ties = 0;
            for (int i = 0; i < spawns.Count; i++)
            {
                float nearest = float.PositiveInfinity;
                foreach (var enemy in enemies) nearest = Math.Min(nearest, spawns[i].DistanceSquared(enemy));

                if (nearest > bestScore)
                {
                    bestScore = nearest;
                    best = i;
                    ties = 1;
                }
                else if (nearest == bestScore && random.Next(++ties) == 0)
                {
                    best = i; // reservoir-sample among equally good spawns
                }
            }
            return best;
        }
    }
}
