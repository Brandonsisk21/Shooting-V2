using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Tufts of alien grass on the arena floor (graphics pass). Blades are placed by raycasting down
    /// onto the "Ground" collider, so nothing grows under cover, on ramps or on the sand path, and
    /// merged into a few meshes per chunk. Visual only: no colliders, no shadows cast.
    /// Density comes from the Graphics quality setting (none on Low).
    /// </summary>
    public static class GrassField
    {
        private const float ChunkSize = 12f;
        private static readonly Color[] Tints =
        {
            Color.Lerp(OutdoorPalette.Grass, Color.white, 0.12f),
            Color.Lerp(OutdoorPalette.Grass, new Color(0.35f, 0.25f, 0.55f), 0.35f),
            Color.Lerp(OutdoorPalette.Grass, new Color(0.95f, 0.6f, 0.8f), 0.25f),
        };

        /// <param name="area">XZ rectangle to cover.</param>
        /// <param name="skip">Returns true for spots that stay bare (e.g. the sand path).</param>
        public static void Build(Transform parent, Rect area, float density, System.Func<Vector2, bool> skip, int seed = 5)
        {
            if (density <= 0f) return;
            var root = new GameObject("Grass").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(seed);
            float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);

            for (float cx = area.xMin; cx < area.xMax; cx += ChunkSize)
            for (float cz = area.yMin; cz < area.yMax; cz += ChunkSize)
            {
                var builders = new MeshBuilder[Tints.Length];
                for (int t = 0; t < builders.Length; t++) builders[t] = new MeshBuilder();

                float w = Mathf.Min(ChunkSize, area.xMax - cx), d = Mathf.Min(ChunkSize, area.yMax - cz);
                // Blades grow in small clumps of ~4, with some bare patches from a low-frequency noise.
                int clumps = Mathf.RoundToInt(w * d * density / 4f);
                for (int c = 0; c < clumps; c++)
                {
                    var spot = new Vector2(cx + Rand(0f, w), cz + Rand(0f, d));
                    if (skip != null && skip(spot)) continue;
                    if (Mathf.PerlinNoise(spot.x * 0.09f + 31f, spot.y * 0.09f + 17f) < 0.3f) continue;
                    if (!Physics.Raycast(new Vector3(spot.x, 30f, spot.y), Vector3.down, out var hit, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.collider.name != "Ground") continue;

                    var tint = builders[rng.Next(builders.Length)];
                    int blades = 3 + rng.Next(3);
                    for (int b = 0; b < blades; b++)
                    {
                        var basePos = hit.point + new Vector3(Rand(-0.18f, 0.18f), 0f, Rand(-0.18f, 0.18f));
                        tint.AddBlade(basePos, Rand(0f, Mathf.PI * 2f), Rand(0.05f, 0.09f), Rand(0.22f, 0.5f), Rand(0.05f, 0.2f));
                    }
                }

                for (int t = 0; t < builders.Length; t++)
                {
                    if (builders[t].Count == 0) continue;
                    var go = new GameObject("GrassChunk");
                    go.transform.SetParent(root, false);
                    go.AddComponent<MeshFilter>().sharedMesh = builders[t].ToMesh();
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = GrayBox.Shiny(Tints[t], 0.08f, 0f);
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = true;
                }
            }
        }

        private sealed class MeshBuilder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<int> _triangles = new List<int>();

            public int Count => _vertices.Count;

            /// <summary>A thin leaning triangle, both faces (the lit shader culls back faces). Normals point up so blades shade like the ground.</summary>
            public void AddBlade(Vector3 basePos, float angle, float width, float height, float lean)
            {
                var side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (width * 0.5f);
                var forward = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Vector3 tip = basePos + Vector3.up * height + forward * lean;
                int i = _vertices.Count;
                _vertices.Add(basePos - side);
                _vertices.Add(basePos + side);
                _vertices.Add(tip);
                for (int n = 0; n < 3; n++) _normals.Add(Vector3.up);
                _triangles.Add(i); _triangles.Add(i + 2); _triangles.Add(i + 1);
                _triangles.Add(i); _triangles.Add(i + 1); _triangles.Add(i + 2);
            }

            public Mesh ToMesh()
            {
                var mesh = new Mesh { name = "Grass" };
                if (_vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
