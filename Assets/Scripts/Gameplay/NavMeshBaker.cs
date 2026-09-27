using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Bakes a navigation mesh at runtime from the map's colliders so bots can path anywhere
    /// without hand-placed waypoints. Uses the built-in AI module (no extra package).
    /// </summary>
    public static class NavMeshBaker
    {
        private static NavMeshDataInstance _instance;

        public static void Bake(Bounds bounds)
        {
            Clear();

            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, Physics.DefaultRaycastLayers, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);
            // Triggers (pickups, hitboxes) and character capsules aren't walkable geometry.
            sources.RemoveAll(s => s.component is Collider c && (c.isTrigger || c is CharacterController));

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.4f;
            settings.agentHeight = 1.8f;
            settings.agentClimb = 0.45f;
            settings.agentSlope = 45f;

            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data != null) _instance = NavMesh.AddNavMeshData(data);
        }

        public static void Clear()
        {
            if (_instance.valid) NavMesh.RemoveNavMeshData(_instance);
            _instance = default;
        }
    }
}
