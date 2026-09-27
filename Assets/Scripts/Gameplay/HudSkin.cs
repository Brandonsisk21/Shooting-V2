using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Procedurally generated HUD textures (rounded panels, soft circles, a damage arc) and IMGUI
    /// drawing helpers, so the HUD has real shapes without any image assets.
    /// </summary>
    public static class HudSkin
    {
        public static readonly Color Panel = new Color(0.05f, 0.08f, 0.12f, 0.62f);
        public static readonly Color PanelEdge = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color Text = new Color(0.95f, 0.97f, 1f);
        public static readonly Color Dim = new Color(1f, 1f, 1f, 0.45f);
        public static readonly Color Accent = new Color(0.45f, 0.85f, 1f);
        public static readonly Color HealthHigh = new Color(0.35f, 0.9f, 0.75f);
        public static readonly Color HealthMid = new Color(1f, 0.82f, 0.25f);
        public static readonly Color HealthLow = new Color(1f, 0.3f, 0.25f);
        public static readonly Color Enemy = new Color(1f, 0.3f, 0.25f);

        private static Texture2D _rounded, _circle, _arc, _vignette;
        private static GUIStyle _roundedStyle;

        public static Texture2D Circle => _circle != null ? _circle : _circle = MakeCircle(64);
        public static Texture2D Arc => _arc != null ? _arc : _arc = MakeArc(128);
        public static Texture2D Vignette => _vignette != null ? _vignette : _vignette = MakeVignette(128);

        /// <summary>A rounded rectangle, 9-sliced so corners stay crisp at any size.</summary>
        public static void RoundedRect(Rect rect, Color color)
        {
            if (_roundedStyle == null)
            {
                _rounded = MakeRounded(32, 10);
                _roundedStyle = new GUIStyle { border = new RectOffset(11, 11, 11, 11) };
                _roundedStyle.normal.background = _rounded;
            }
            if (Event.current.type != EventType.Repaint) return;
            Color saved = GUI.color;
            GUI.color = color;
            _roundedStyle.Draw(rect, false, false, false, false);
            GUI.color = saved;
        }

        public static void Fill(Rect rect, Color color) => DrawTexture(rect, Texture2D.whiteTexture, color);

        public static void DrawTexture(Rect rect, Texture texture, Color color)
        {
            Color saved = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, texture);
            GUI.color = saved;
        }

        public static void Label(Rect rect, string text, GUIStyle style, Color color, TextAnchor anchor, bool shadow = true)
        {
            TextAnchor savedAnchor = style.alignment;
            style.alignment = anchor;
            Color saved = GUI.color;
            if (shadow)
            {
                GUI.color = new Color(0f, 0f, 0f, color.a * 0.6f);
                GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, style);
            }
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = saved;
            style.alignment = savedAnchor;
        }

        private static Texture2D MakeRounded(int size, float radius)
        {
            var tex = NewTexture(size, "HudRounded");
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                float dy = Mathf.Max(0f, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                float a = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeCircle(int size)
        {
            var tex = NewTexture(size, "HudCircle");
            var px = new Color[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d));
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>A thick arc at the top of the texture (pointing "up"); rotate it to point at a threat.</summary>
        private static Texture2D MakeArc(int size)
        {
            var tex = NewTexture(size, "HudArc");
            var px = new Color[size * size];
            float c = size * 0.5f, outer = size * 0.5f, inner = size * 0.4f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f - c, y + 0.5f - c);
                float d = p.magnitude;
                float angle = Mathf.Abs(Vector2.SignedAngle(Vector2.up, p));
                float ring = Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner);
                float spread = Mathf.Clamp01((35f - angle) / 10f);
                px[y * size + x] = new Color(1f, 1f, 1f, ring * spread);
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeVignette(int size)
        {
            var tex = NewTexture(size, "HudVignette");
            var px = new Color[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) / c;
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((d - 0.55f) / 0.5f));
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D NewTexture(int size, string name) =>
            new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
    }
}
