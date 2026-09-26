using System.Collections.Generic;
using ArenaShooter.Core;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Gray-box HUD drawn with IMGUI (GDD 7): health, ammo, crosshair, hit marker, plus scope
    /// overlay, pickup prompt, damage numbers and the sniper spawn timer.
    /// </summary>
    public class PlayerHud : MonoBehaviour
    {
        public PlayerController player;
        public WeaponHolder weapons;
        public Health health;
        public Camera view;
        public SniperSpawnPad sniperPad;

        private const float HitMarkerDuration = 0.25f;
        private const float DamageNumberDuration = 0.8f;

        private static readonly Color HeadColor = new Color(1f, 0.85f, 0.2f);
        private static readonly Color KillColor = new Color(1f, 0.25f, 0.2f);

        private struct DamageNumber
        {
            public Vector3 WorldPos;
            public string Text;
            public Color Color;
            public float Time;
        }

        private readonly List<DamageNumber> _numbers = new List<DamageNumber>();
        private float _hitMarkerTime = float.NegativeInfinity;
        private Color _hitMarkerColor = Color.white;
        private bool _showHelp = true;
        private GUIStyle _label, _big, _center, _small;

        private void Start()
        {
            weapons.HitConfirmed += OnHit;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) _showHelp = !_showHelp;
            _numbers.RemoveAll(n => Time.time - n.Time > DamageNumberDuration);
        }

        private void OnHit(HitInfo hit)
        {
            _hitMarkerTime = Time.time;
            _hitMarkerColor = hit.Damage.Killed ? KillColor : hit.Zone == HitZone.Head ? HeadColor : Color.white;
            _numbers.Add(new DamageNumber
            {
                WorldPos = hit.Point,
                Text = Mathf.RoundToInt(hit.Damage.Applied) + (hit.Zone == HitZone.Head ? " HEAD" : ""),
                Color = _hitMarkerColor,
                Time = Time.time,
            });
        }

        private void OnGUI()
        {
            EnsureStyles();
            float w = Screen.width, h = Screen.height;
            var center = new Vector2(w * 0.5f, h * 0.5f);

            if (player.IsDead)
            {
                Label(new Rect(0, h * 0.4f, w, 50), $"You died. Respawning in {player.RespawnCountdown:0.0}s", _big, KillColor);
                return;
            }

            if (weapons.IsZoomed) DrawScope(center, w, h);
            else DrawCrosshair(center);
            DrawHitMarker(center);
            DrawDamageNumbers();
            DrawHealth(h);
            DrawAmmo(w, h);
            DrawPickupPrompt(w, h);
            DrawSniperTimer(w);

            if (Cursor.lockState != CursorLockMode.Locked)
                Label(new Rect(0, h * 0.3f, w, 50), "Click to play", _big, Color.white);
            if (_showHelp)
                GUI.Label(new Rect(12, 10, 460, 200),
                    "WASD move   Space jump   Mouse aim\n" +
                    "LMB fire   RMB scope (sniper)   R reload\n" +
                    "E pick up   Q / wheel / 1 / 2 switch weapon\n" +
                    "K hurt yourself (test regen)   Esc release mouse\n" +
                    "F1 hide this help", _small);
        }

        private void DrawCrosshair(Vector2 c)
        {
            const float gap = 4f, len = 7f, thick = 2f;
            var col = new Color(1f, 1f, 1f, 0.9f);
            Fill(new Rect(c.x - gap - len, c.y - thick / 2, len, thick), col);
            Fill(new Rect(c.x + gap, c.y - thick / 2, len, thick), col);
            Fill(new Rect(c.x - thick / 2, c.y - gap - len, thick, len), col);
            Fill(new Rect(c.x - thick / 2, c.y + gap, thick, len), col);
            Fill(new Rect(c.x - 1, c.y - 1, 2, 2), col);
        }

        private void DrawScope(Vector2 c, float w, float h)
        {
            float size = h * 0.9f;
            var black = new Color(0f, 0f, 0f, 0.92f);
            Fill(new Rect(0, 0, c.x - size / 2, h), black);
            Fill(new Rect(c.x + size / 2, 0, w - (c.x + size / 2), h), black);
            Fill(new Rect(c.x - size / 2, 0, size, (h - size) / 2), black);
            Fill(new Rect(c.x - size / 2, c.y + size / 2, size, (h - size) / 2), black);
            var line = new Color(0f, 0f, 0f, 0.85f);
            Fill(new Rect(c.x - size / 2, c.y - 0.5f, size, 1f), line);
            Fill(new Rect(c.x - 0.5f, c.y - size / 2, 1f, size), line);
            Fill(new Rect(c.x - 2, c.y - 2, 4, 4), new Color(1f, 0.2f, 0.2f, 0.9f));
            Label(new Rect(c.x + 12, c.y + 12, 60, 20), $"{weapons.CurrentZoom:0.#}x", _small, Color.white);
        }

        private void DrawHitMarker(Vector2 c)
        {
            float age = Time.time - _hitMarkerTime;
            if (age > HitMarkerDuration) return;
            var col = _hitMarkerColor;
            col.a = 1f - age / HitMarkerDuration;
            const float inner = 6f, len = 8f, thick = 2f;
            for (int i = 0; i < 4; i++)
            {
                Matrix4x4 saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f + 90f * i, c);
                Fill(new Rect(c.x + inner, c.y - thick / 2, len, thick), col);
                GUI.matrix = saved;
            }
        }

        private void DrawDamageNumbers()
        {
            foreach (var n in _numbers)
            {
                float t = (Time.time - n.Time) / DamageNumberDuration;
                Vector3 screen = view.WorldToScreenPoint(n.WorldPos + Vector3.up * (0.3f + t));
                if (screen.z <= 0f) continue;
                var col = n.Color;
                col.a = 1f - t;
                Label(new Rect(screen.x - 60, FlipY(screen.y) - 12, 120, 24), n.Text, _center, col);
            }
        }

        private void DrawHealth(float h)
        {
            var model = health.Model;
            var rect = new Rect(20, h - 44, 240, 22);
            Fill(rect, new Color(0f, 0f, 0f, 0.5f));
            var fill = Color.Lerp(new Color(0.9f, 0.2f, 0.15f), new Color(0.3f, 0.85f, 0.4f), model.Fraction);
            Fill(new Rect(rect.x + 2, rect.y + 2, (rect.width - 4) * model.Fraction, rect.height - 4), fill);
            Label(new Rect(rect.x + 8, rect.y + 1, rect.width, rect.height), $"HP {Mathf.CeilToInt(model.Current)}", _label, Color.white);
        }

        private void DrawAmmo(float w, float h)
        {
            var loadout = weapons.Loadout;
            var active = loadout.Active;
            if (active == null) return;

            string reserve = active.Stats.HasUnlimitedReserve ? "∞" : active.Reserve.ToString();
            Label(new Rect(w - 280, h - 70, 260, 34), $"{active.Magazine} / {reserve}", _big, Color.white, TextAnchor.MiddleRight);
            string status = active.IsReloading ? "  RELOADING" : loadout.IsSwitching ? "  ..." : "";
            Label(new Rect(w - 280, h - 38, 260, 22), active.Stats.displayName.ToUpperInvariant() + status, _label, Color.white, TextAnchor.MiddleRight);

            if (active.IsReloading)
            {
                var bar = new Rect(w * 0.5f - 60, h * 0.5f + 30, 120, 4);
                Fill(bar, new Color(0f, 0f, 0f, 0.5f));
                Fill(new Rect(bar.x, bar.y, bar.width * active.ReloadProgress, bar.height), Color.white);
            }

            for (int i = 0; i < loadout.Slots.Count; i++)
            {
                if (i == loadout.ActiveIndex) continue;
                var other = loadout.Slots[i];
                string otherReserve = other.Stats.HasUnlimitedReserve ? "∞" : other.Reserve.ToString();
                Label(new Rect(w - 280, h - 96, 260, 22), $"[{i + 1}] {other.Stats.displayName} {other.Magazine}/{otherReserve}", _small, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleRight);
            }
        }

        private void DrawPickupPrompt(float w, float h)
        {
            var pickup = weapons.NearbyPickup;
            if (pickup == null) return;
            string name = pickup.Weapon.Stats.displayName;
            string text;
            switch (weapons.NearbyPickupOutcome)
            {
                case PickupOutcome.Added: text = $"Press E to pick up {name}"; break;
                case PickupOutcome.Swapped: text = $"Press E to swap {weapons.Loadout.Active.Stats.displayName} for {name}"; break;
                case PickupOutcome.AmmoTaken: text = $"Press E to take {name} ammo"; break;
                default: return;
            }
            Label(new Rect(0, h * 0.62f, w, 30), text, _label, Color.white, TextAnchor.MiddleCenter);
        }

        private void DrawSniperTimer(float w)
        {
            if (sniperPad == null) return;
            string text = sniperPad.HasWeaponOnPad
                ? $"Sniper on pad (refresh in {Format(sniperPad.TimeUntilNextSpawn)})"
                : $"Sniper spawns in {Format(sniperPad.TimeUntilNextSpawn)}";
            Label(new Rect(0, 10, w, 24), text, _label, Color.white, TextAnchor.MiddleCenter);
        }

        private static string Format(float seconds)
        {
            int s = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        private static float FlipY(float screenY) => Screen.height - screenY;

        private static void Fill(Rect rect, Color color)
        {
            Color saved = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = saved;
        }

        private static void Label(Rect rect, string text, GUIStyle style, Color color, TextAnchor? anchor = null)
        {
            TextAnchor savedAnchor = style.alignment;
            if (anchor.HasValue) style.alignment = anchor.Value;
            Color saved = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, color.a * 0.7f);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, style);
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = saved;
            style.alignment = savedAnchor;
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _label.normal.textColor = Color.white;
            _big = new GUIStyle(_label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            _small = new GUIStyle(_label) { fontSize = 13, fontStyle = FontStyle.Normal, alignment = TextAnchor.UpperLeft };
        }
    }
}
