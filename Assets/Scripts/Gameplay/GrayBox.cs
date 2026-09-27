using System.Collections.Generic;
using ArenaShooter.Core;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Helpers for building placeholder geometry from primitives (GDD 5: Phase 1 is gray-box only).
    /// Everything visual lives here so gameplay code never depends on art.
    /// </summary>
    public static class GrayBox
    {
        public static readonly Color Floor = new Color(0.55f, 0.57f, 0.6f);
        public static readonly Color Wall = new Color(0.42f, 0.44f, 0.48f);
        public static readonly Color Cover = new Color(0.33f, 0.47f, 0.62f);
        public static readonly Color Platform = new Color(0.62f, 0.52f, 0.36f);

        private static readonly Dictionary<(Color, Vector2), Material> Materials = new Dictionary<(Color, Vector2), Material>();
        private static Texture2D _grid;
        private const float GlowBoost = 2.2f;
        private static Material _hdrVertexColor;

        /// <summary>Unlit vertex-colored material brightened for bloom (bolts, sparks).</summary>
        public static Material HdrVertexColor
        {
            get
            {
                if (_hdrVertexColor == null) _hdrVertexColor = new Material(VertexColorUnlit) { color = new Color(GlowBoost, GlowBoost, GlowBoost, 1f) };
                return _hdrVertexColor;
            }
        }
        private static Material _vertexColorUnlit;
        private static readonly Dictionary<Color, Material> GlassMaterials = new Dictionary<Color, Material>();
        private static readonly Dictionary<Color, Material> GlowMaterials = new Dictionary<Color, Material>();

        public static Material Mat(Color color, Vector2? gridTiling = null)
        {
            Vector2 tiling = gridTiling ?? Vector2.zero;
            if (Materials.TryGetValue((color, tiling), out var cached) && cached != null) return cached;

            Material mat;
            SurfaceKind? surface = gridTiling.HasValue ? SurfaceFor(color) : null;
            if (surface.HasValue)
            {
                // Realistic surfaces: tiling detail texture + normal map, per-surface shininess.
                var (albedo, normal) = SurfaceTextures.Get(surface.Value);
                mat = NewLitNormal(color);
                mat.mainTexture = albedo;
                float scale = surface.Value == SurfaceKind.Ground ? 0.25f : surface.Value == SurfaceKind.Rock ? 0.5f : 1f;
                mat.mainTextureScale = tiling * scale;
                if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", normal);
                SetShine(mat, surface.Value == SurfaceKind.Plating ? 0.42f : surface.Value == SurfaceKind.Rock ? 0.14f : 0.06f,
                    surface.Value == SurfaceKind.Plating ? 0.15f : 0f);
            }
            else
            {
                mat = NewLit(color);
                if (gridTiling.HasValue)
                {
                    mat.mainTexture = GridTexture(); // test range keeps its measuring grid
                    mat.mainTextureScale = tiling;
                }
                SetShine(mat, 0.1f, 0f);
            }
            Materials[(color, tiling)] = mat;
            return mat;
        }

        /// <summary>A lit material with explicit shininess (models: plastic armor, metal, skin...).</summary>
        public static Material Shiny(Color color, float gloss, float metallic)
        {
            var key = (color, new Vector2(-1f - gloss, -1f - metallic)); // distinct from tiled keys
            if (Materials.TryGetValue(key, out var cached) && cached != null) return cached;
            var mat = NewLit(color);
            SetShine(mat, gloss, metallic);
            Materials[key] = mat;
            return mat;
        }

        private static void SetShine(Material mat, float gloss, float metallic)
        {
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", gloss);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        }

        /// <summary>Which realistic surface a palette color gets (null: plain / grid).</summary>
        private static SurfaceKind? SurfaceFor(Color c)
        {
            if (c == OutdoorPalette.Grass || c == OutdoorPalette.Hills) return SurfaceKind.Ground;
            if (c == OutdoorPalette.Rock || c == OutdoorPalette.RockLight || c == OutdoorPalette.Cliff || c == OutdoorPalette.CliffLight) return SurfaceKind.Rock;
            if (c == Floor || c == Wall || c == Cover || c == Platform) return null; // test range
            return SurfaceKind.Plating;
        }

        /// <summary>Lit material with a normal map (template keeps the normal-map shader variant in builds).</summary>
        public static Material NewLitNormal(Color color)
        {
            var template = Resources.Load<Material>("ArenaMaterials/LitNormal");
            var mat = template != null ? new Material(template) : NewLit(color);
            if (template == null) mat.EnableKeyword("_NORMALMAP");
            mat.color = color;
            return mat;
        }

        /// <summary>A solid box whose grid texture tiles once per meter along its two largest dimensions.</summary>
        public static GameObject Box(string name, Vector3 center, Vector3 size, Color color, Transform parent, Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rotation ?? Quaternion.identity);
            go.transform.localScale = size;

            float[] dims = { size.x, size.y, size.z };
            System.Array.Sort(dims);
            var tiling = new Vector2(Mathf.Round(dims[2]), Mathf.Round(dims[1]));
            go.GetComponent<Renderer>().sharedMaterial = Mat(color, tiling);
            return go;
        }

        /// <summary>A walkable ramp whose top surface runs from <paramref name="low"/> to <paramref name="high"/>.</summary>
        public static GameObject Ramp(string name, Vector3 low, Vector3 high, float width, Color color, Transform parent)
        {
            const float thickness = 0.4f;
            Vector3 dir = high - low;
            var rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            Vector3 center = (low + high) * 0.5f - rotation * Vector3.up * (thickness * 0.5f);
            return Box(name, center, new Vector3(width, thickness, dir.magnitude), color, parent, rotation);
        }

        /// <summary>A solid (collidable) primitive of any shape, with a flat color.</summary>
        public static GameObject Solid(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Color color, Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation ?? Quaternion.identity);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            return go;
        }

        /// <summary>An invisible wall: collider only.</summary>
        public static GameObject Blocker(string name, Vector3 center, Vector3 size, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        /// <summary>A primitive with no collider, for visuals only.</summary>
        public static GameObject Visual(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            // Immediate so the collider never exists for a frame (it would block the player or eat shots).
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            return go;
        }

        /// <summary>
        /// A lit material. Cloned from a template in Resources so the Standard shader is included
        /// in player builds (Shader.Find alone only works in the editor for unreferenced shaders).
        /// </summary>
        public static Material NewLit(Color color)
        {
            var template = Resources.Load<Material>("ArenaMaterials/Lit");
            Material mat;
            if (template != null) mat = new Material(template);
            else
            {
                var shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader);
            }
            mat.color = color;
            return mat;
        }

        /// <summary>Glassy see-through material (helmet bubbles, visors). Cached per color.</summary>
        public static Material Glass(Color color)
        {
            if (GlassMaterials.TryGetValue(color, out var cached) && cached != null) return cached;
            var template = Resources.Load<Material>("ArenaMaterials/LitTransparent");
            var mat = template != null ? new Material(template) : NewLit(color);
            mat.color = color;
            GlassMaterials[color] = mat;
            return mat;
        }

        /// <summary>
        /// Flat, unlit, fog-free color (glows, sky objects, effects). Cached per color. Brightened past
        /// 1.0 (HDR) so post-processing bloom makes it glow; looks the same without post effects.
        /// </summary>
        public static Material Glow(Color color)
        {
            if (GlowMaterials.TryGetValue(color, out var cached) && cached != null) return cached;
            var mat = new Material(VertexColorUnlit) { color = new Color(color.r * GlowBoost, color.g * GlowBoost, color.b * GlowBoost, color.a) };
            GlowMaterials[color] = mat;
            return mat;
        }

        /// <summary>A primitive with no collider using a glow material.</summary>
        public static GameObject GlowVisual(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = Visual(type, name, parent, localPos, localScale, color);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = Glow(color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Shared unlit material that takes its color from vertex colors (e.g. LineRenderer colors).</summary>
        public static Material VertexColorUnlit
        {
            get
            {
                if (_vertexColorUnlit == null) _vertexColorUnlit = new Material(Shader.Find("Sprites/Default"));
                return _vertexColorUnlit;
            }
        }

        private static Texture2D GridTexture()
        {
            if (_grid != null) return _grid;
            const int size = 64;
            _grid = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "GrayBoxGrid",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool line = x < 2 || y < 2;
                pixels[y * size + x] = line ? new Color(0.75f, 0.75f, 0.75f) : Color.white;
            }
            _grid.SetPixels(pixels);
            _grid.Apply(true);
            return _grid;
        }
    }
}
