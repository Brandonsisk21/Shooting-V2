using System.Collections.Generic;
using ArenaShooter.Core;
using ArenaShooter.Gameplay.Net;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// The player's HUD (GDD 7), drawn with IMGUI and procedural textures:
    /// bottom-left status panel (weapon, ammo count + round pips, segmented health bar), crosshair
    /// that turns red over enemies, hit markers, damage numbers, damage-direction arcs, low-health
    /// vignette, kill feed, score widget, scoreboard, death card and match results.
    /// </summary>
    public class PlayerHud : MonoBehaviour
    {
        public PlayerController player;
        public Combatant combatant;
        public WeaponHolder weapons;
        public Health health;
        public PlayerInputReader input;
        public Camera view;
        public SniperSpawnPad sniperPad;
        public string mapName = "";

        private const float HitMarkerDuration = 0.25f;
        private const float DamageNumberDuration = 0.8f;
        private const float DamageArcDuration = 1.2f;
        private const float FeedDuration = 6f;
        private const float Margin = 24f;

        private struct DamageNumber
        {
            public Vector3 WorldPos;
            public string Text;
            public Color Color;
            public float Time;
        }

        private struct DamageArc
        {
            public Vector3 From;
            public float Time;
        }

        private readonly List<DamageNumber> _numbers = new List<DamageNumber>();
        private readonly List<DamageArc> _arcs = new List<DamageArc>();
        private float _hitMarkerTime = float.NegativeInfinity;
        private Color _hitMarkerColor = Color.white;
        private string _notice = "";
        private Color _noticeColor = Color.white;
        private float _noticeTime = float.NegativeInfinity;
        private KillEvent? _killedBy;
        private float _lastHealth;
        private float _regenSeenAt = float.NegativeInfinity;
        private bool _showHelp = GameSettings.ShowControlsHint;
        private GUIStyle _small, _label, _medium, _big, _title;

        private static readonly Color HeadColor = new Color(1f, 0.85f, 0.2f);

        private void Start()
        {
            weapons.HitConfirmed += OnHit;
            health.Damaged += OnDamaged;
            if (MatchManager.Current != null) MatchManager.Current.Killed += OnKill;
            combatant.Respawned += _ => _killedBy = null;
            _lastHealth = health.Current;
        }

        private void OnEnable()
        {
            GameSettings.Changed += OnSettingsChanged;
            NetSession.Notice += OnNetNotice;
        }

        private void OnDisable()
        {
            GameSettings.Changed -= OnSettingsChanged;
            NetSession.Notice -= OnNetNotice;
        }

        private void OnNetNotice(string text)
        {
            _notice = text;
            _noticeColor = HudSkin.Accent;
            _noticeTime = Time.time;
        }

        private void OnSettingsChanged() => _showHelp = GameSettings.ShowControlsHint;

        private void OnDestroy()
        {
            if (MatchManager.Current != null) MatchManager.Current.Killed -= OnKill;
        }

        private void Update()
        {
            if (player.LastCommands.ToggleHelp)
            {
                _showHelp = !_showHelp;
                GameSettings.ShowControlsHint = _showHelp;
                GameSettings.Save();
            }
            _numbers.RemoveAll(n => Time.time - n.Time > DamageNumberDuration);
            _arcs.RemoveAll(a => Time.time - a.Time > DamageArcDuration);
            if (health.Current > _lastHealth + 0.01f) _regenSeenAt = Time.time;
            _lastHealth = health.Current;
        }

        private void OnHit(HitInfo hit)
        {
            _hitMarkerTime = Time.time;
            _hitMarkerColor = hit.Damage.Killed ? HudSkin.Enemy : hit.Zone == HitZone.Head ? HeadColor : Color.white;
            _numbers.Add(new DamageNumber
            {
                WorldPos = hit.Point,
                Text = Mathf.RoundToInt(hit.Damage.Applied).ToString(),
                Color = _hitMarkerColor,
                Time = Time.time,
            });
        }

        private void OnDamaged(Health h, DamageResult result, DamageSource source)
        {
            if (source.Instigator != null && source.Instigator != combatant)
                _arcs.Add(new DamageArc { From = source.Instigator.transform.position, Time = Time.time });
        }

        private void OnKill(KillEvent kill)
        {
            if (kill.Victim == combatant)
            {
                _killedBy = kill;
            }
            else if (kill.Killer == combatant)
            {
                _notice = (kill.Headshot ? "Headshot! " : "") + "You killed " + kill.Victim.displayName;
                _noticeColor = kill.Victim.color;
                _noticeTime = Time.time;
            }
        }

        // ================================================================= drawing

        private void OnGUI()
        {
            EnsureStyles();
            float w = Screen.width, h = Screen.height;
            var center = new Vector2(w * 0.5f, h * 0.5f);
            var match = MatchManager.Current;
            bool gamepad = input != null && input.ActiveDevice == InputDeviceKind.Gamepad;

            DrawLowHealthVignette(w, h);
            DrawDamageArcs(center, h);

            if (player.IsDead)
            {
                DrawDeathCard(w, h, match);
            }
            else
            {
                if (weapons.IsZoomed) DrawScope(center, w, h);
                else DrawCrosshair(center);
                DrawHitMarker(center);
                DrawDamageNumbers();
                DrawNotice(w, h);
                DrawAimName(center);
                DrawStatusPanel(h, gamepad);
                DrawPickupPrompt(w, h, gamepad);
            }

            DrawKillFeed(w, match);
            DrawScoreWidget(w, h, match);
            DrawSniperTimer(w);
            DrawOnlineBadge(w);

            bool matchOver = match != null && match.IsOver;
            if (matchOver) DrawResults(w, h, match);
            else if (player.LastCommands.ScoreboardHeld && match != null) DrawScoreboard(w, h * 0.22f, match);

            if (_showHelp && !matchOver) DrawHelp(gamepad);
            if (Cursor.lockState != CursorLockMode.Locked && !gamepad && !ArenaBootstrap.IsPaused)
                HudSkin.Label(new Rect(0, h * 0.3f, w, 50), "Click to play", _big, Color.white, TextAnchor.MiddleCenter);
        }

        // ---------------------------------------------------------------- status panel (bottom-left)

        private void DrawStatusPanel(float h, bool gamepad)
        {
            var loadout = weapons.Loadout;
            var active = loadout.Active;
            if (active == null) return;

            var panel = new Rect(Margin, h - Margin - 118f, 372f, 118f);
            HudSkin.RoundedRect(panel, HudSkin.Panel);
            float x = panel.x + 16f, right = panel.xMax - 16f;

            // Row 1: weapon name + the other slot.
            string name = active.Stats.displayName.ToUpperInvariant();
            if (loadout.IsSwitching) name += "  ...";
            HudSkin.Label(new Rect(x, panel.y + 10f, 200f, 22f), name, _label, HudSkin.Accent, TextAnchor.MiddleLeft);
            for (int i = 0; i < loadout.Slots.Count; i++)
            {
                if (i == loadout.ActiveIndex) continue;
                var other = loadout.Slots[i];
                string key = gamepad ? "Y" : "Q";
                HudSkin.Label(new Rect(right - 200f, panel.y + 10f, 200f, 22f),
                    $"[{key}] {other.Stats.displayName.ToUpperInvariant()}  {other.Magazine}/{ReserveText(other)}", _small, HudSkin.Dim, TextAnchor.MiddleRight);
            }

            // Row 2: big magazine count, reserve, and round pips.
            float lowAmmo = active.Stats.magazineSize * 0.25f;
            Color magColor = active.Magazine == 0 ? HudSkin.HealthLow : active.Magazine <= lowAmmo ? HudSkin.HealthMid : HudSkin.Text;
            HudSkin.Label(new Rect(x - 2f, panel.y + 30f, 90f, 46f), active.Magazine.ToString(), _big, magColor, TextAnchor.MiddleLeft);
            HudSkin.Label(new Rect(x + 62f, panel.y + 42f, 80f, 26f), "/ " + ReserveText(active), _medium, HudSkin.Dim, TextAnchor.MiddleLeft);
            DrawAmmoPips(new Rect(x + 140f, panel.y + 36f, right - x - 140f, 36f), active);

            // Row 3: segmented health bar + number.
            DrawHealthBar(new Rect(x, panel.y + 88f, right - x - 44f, 16f));
            HudSkin.Label(new Rect(right - 40f, panel.y + 84f, 40f, 24f), Mathf.CeilToInt(health.Current).ToString(), _label, HealthColor(), TextAnchor.MiddleRight);
        }

        private void DrawAmmoPips(Rect area, WeaponState weapon)
        {
            int mag = weapon.Stats.magazineSize;
            if (weapon.IsReloading)
            {
                HudSkin.Label(new Rect(area.x, area.y, area.width, 20f), "RELOADING", _small, HudSkin.Text, TextAnchor.MiddleLeft);
                var bar = new Rect(area.x, area.y + 24f, area.width, 6f);
                HudSkin.RoundedRect(bar, new Color(1f, 1f, 1f, 0.15f));
                HudSkin.Fill(new Rect(bar.x, bar.y, bar.width * weapon.ReloadProgress, bar.height), HudSkin.Accent);
                return;
            }

            bool big = mag <= 10;
            int perRow = big ? mag : Mathf.CeilToInt(mag / 2f);
            int rows = big ? 1 : 2;
            float gap = big ? 6f : 3f;
            float pipW = Mathf.Min(big ? 16f : 9f, (area.width - gap * (perRow - 1)) / perRow);
            float pipH = big ? area.height : (area.height - gap) / 2f;
            for (int i = 0; i < mag; i++)
            {
                int row = i / perRow, col = i % perRow;
                var r = new Rect(area.x + col * (pipW + gap), area.y + row * (pipH + gap), pipW, pipH);
                bool loaded = i < weapon.Magazine;
                HudSkin.Fill(r, loaded ? new Color(1f, 0.9f, 0.6f, 0.95f) : new Color(1f, 1f, 1f, 0.12f));
            }
            if (weapon.Magazine == 0 && !weapon.IsEmpty)
                HudSkin.Label(area, "RELOAD", _label, HudSkin.HealthLow, TextAnchor.MiddleCenter);
            else if (weapon.IsEmpty)
                HudSkin.Label(area, "EMPTY", _label, HudSkin.HealthLow, TextAnchor.MiddleCenter);
        }

        private void DrawHealthBar(Rect bar)
        {
            const int segments = 10;
            const float gap = 3f;
            float segW = (bar.width - gap * (segments - 1)) / segments;
            float perSegment = health.Model.Max / segments;
            bool regenerating = Time.time - _regenSeenAt < 0.2f;
            Color color = HealthColor();
            if (regenerating) color = Color.Lerp(color, Color.white, 0.35f + 0.25f * Mathf.Sin(Time.time * 12f));

            for (int i = 0; i < segments; i++)
            {
                var seg = new Rect(bar.x + i * (segW + gap), bar.y, segW, bar.height);
                HudSkin.Fill(seg, new Color(1f, 1f, 1f, 0.12f));
                float fill = Mathf.Clamp01((health.Current - i * perSegment) / perSegment);
                if (fill > 0f) HudSkin.Fill(new Rect(seg.x, seg.y, seg.width * fill, seg.height), color);
            }
        }

        private Color HealthColor()
        {
            float f = health.Model.Fraction;
            if (f > 0.6f) return HudSkin.HealthHigh;
            if (f > 0.3f) return HudSkin.HealthMid;
            return Color.Lerp(HudSkin.HealthLow, Color.white, 0.25f * (0.5f + 0.5f * Mathf.Sin(Time.time * 10f)));
        }

        private static string ReserveText(WeaponState w) => w.Stats.HasUnlimitedReserve ? "∞" : w.Reserve.ToString();

        // ---------------------------------------------------------------- center screen

        private void DrawCrosshair(Vector2 c)
        {
            bool onEnemy = weapons.AimTarget != null;
            Color col = onEnemy ? HudSkin.Enemy : new Color(1f, 1f, 1f, 0.9f);
            bool sniper = weapons.Loadout.Active != null && weapons.Loadout.Active.Stats.id == "sniper";
            if (sniper)
            {
                HudSkin.DrawTexture(new Rect(c.x - 3f, c.y - 3f, 6f, 6f), HudSkin.Circle, col);
                return;
            }
            const float gap = 5f, len = 8f, thick = 2f;
            HudSkin.Fill(new Rect(c.x - gap - len, c.y - thick / 2, len, thick), col);
            HudSkin.Fill(new Rect(c.x + gap, c.y - thick / 2, len, thick), col);
            HudSkin.Fill(new Rect(c.x - thick / 2, c.y - gap - len, thick, len), col);
            HudSkin.Fill(new Rect(c.x - thick / 2, c.y + gap, thick, len), col);
            HudSkin.DrawTexture(new Rect(c.x - 1.5f, c.y - 1.5f, 3f, 3f), HudSkin.Circle, col);
        }

        private void DrawScope(Vector2 c, float w, float h)
        {
            float size = h * 0.9f;
            var black = new Color(0f, 0f, 0f, 0.92f);
            HudSkin.Fill(new Rect(0, 0, c.x - size / 2, h), black);
            HudSkin.Fill(new Rect(c.x + size / 2, 0, w - (c.x + size / 2), h), black);
            HudSkin.Fill(new Rect(c.x - size / 2, 0, size, (h - size) / 2), black);
            HudSkin.Fill(new Rect(c.x - size / 2, c.y + size / 2, size, (h - size) / 2), black);
            var line = new Color(0f, 0f, 0f, 0.85f);
            HudSkin.Fill(new Rect(c.x - size / 2, c.y - 0.5f, size, 1f), line);
            HudSkin.Fill(new Rect(c.x - 0.5f, c.y - size / 2, 1f, size), line);
            Color dot = weapons.AimTarget != null ? HudSkin.Enemy : new Color(1f, 0.2f, 0.2f, 0.9f);
            HudSkin.DrawTexture(new Rect(c.x - 3, c.y - 3, 6, 6), HudSkin.Circle, dot);
            HudSkin.Label(new Rect(c.x + 14, c.y + 12, 60, 20), $"{weapons.CurrentZoom:0.#}x", _small, Color.white, TextAnchor.MiddleLeft);
        }

        private void DrawHitMarker(Vector2 c)
        {
            float age = Time.time - _hitMarkerTime;
            if (age > HitMarkerDuration) return;
            var col = _hitMarkerColor;
            col.a = 1f - age / HitMarkerDuration;
            const float inner = 7f, len = 9f, thick = 2f;
            for (int i = 0; i < 4; i++)
            {
                Matrix4x4 saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f + 90f * i, c);
                HudSkin.Fill(new Rect(c.x + inner, c.y - thick / 2, len, thick), col);
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
                HudSkin.Label(new Rect(screen.x - 60, Screen.height - screen.y - 12, 120, 24), n.Text, _label, col, TextAnchor.MiddleCenter);
            }
        }

        private void DrawNotice(float w, float h)
        {
            float age = Time.time - _noticeTime;
            if (age > 2f) return;
            var col = _noticeColor;
            col.a = Mathf.Clamp01(2f - age);
            HudSkin.Label(new Rect(0, h * 0.5f + 40f, w, 26f), _notice, _label, col, TextAnchor.MiddleCenter);
        }

        private void DrawDamageArcs(Vector2 c, float h)
        {
            if (_arcs.Count == 0) return;
            Vector3 forward = combatant.transform.forward;
            forward.y = 0f;
            float size = h * 0.42f;
            foreach (var arc in _arcs)
            {
                Vector3 to = arc.From - combatant.transform.position;
                to.y = 0f;
                if (to.sqrMagnitude < 0.01f) continue;
                float angle = Vector3.SignedAngle(forward, to, Vector3.up);
                float alpha = 1f - (Time.time - arc.Time) / DamageArcDuration;
                Matrix4x4 saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(angle, c);
                HudSkin.DrawTexture(new Rect(c.x - size / 2, c.y - size / 2, size, size), HudSkin.Arc, new Color(1f, 0.2f, 0.15f, 0.8f * alpha));
                GUI.matrix = saved;
            }
        }

        private void DrawLowHealthVignette(float w, float h)
        {
            if (player.IsDead) return;
            float f = health.Model.Fraction;
            if (f >= 0.35f) return;
            float strength = (0.35f - f) / 0.35f;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);
            HudSkin.DrawTexture(new Rect(0, 0, w, h), HudSkin.Vignette, new Color(0.8f, 0.05f, 0.05f, 0.6f * strength * pulse));
        }

        private void DrawPickupPrompt(float w, float h, bool gamepad)
        {
            var pickup = weapons.NearbyPickup;
            if (pickup == null) return;
            string weaponName = pickup.Weapon.Stats.displayName;
            string verb = gamepad ? "Hold X to" : "Press E to";
            string text;
            switch (weapons.NearbyPickupOutcome)
            {
                case PickupOutcome.Added: text = $"{verb} pick up {weaponName}"; break;
                case PickupOutcome.Swapped: text = $"{verb} swap {weapons.Loadout.Active.Stats.displayName} for {weaponName}"; break;
                case PickupOutcome.AmmoTaken: text = $"{verb} take {weaponName} ammo"; break;
                default: return;
            }
            var size = _label.CalcSize(new GUIContent(text));
            var r = new Rect((w - size.x) / 2f - 14f, h * 0.64f, size.x + 28f, 32f);
            HudSkin.RoundedRect(r, HudSkin.Panel);
            HudSkin.Label(r, text, _label, HudSkin.Text, TextAnchor.MiddleCenter, shadow: false);
        }

        private void DrawDeathCard(float w, float h, MatchManager match)
        {
            var card = new Rect(w / 2f - 190f, h * 0.36f, 380f, 96f);
            HudSkin.RoundedRect(card, HudSkin.Panel);
            string by = "You died";
            Color byColor = HudSkin.Text;
            if (_killedBy.HasValue && _killedBy.Value.Killer != null)
            {
                var k = _killedBy.Value;
                by = $"Killed by {k.Killer.displayName}" + (k.Headshot ? " (headshot)" : "");
                byColor = k.Killer.color;
            }
            HudSkin.Label(new Rect(card.x, card.y + 14f, card.width, 36f), by, _medium, byColor, TextAnchor.MiddleCenter);
            float respawn = match != null ? match.RespawnCountdown(combatant) : 0f;
            HudSkin.Label(new Rect(card.x, card.y + 54f, card.width, 26f), $"Respawning in {respawn:0.0}s", _label, HudSkin.Dim, TextAnchor.MiddleCenter);
        }

        // ---------------------------------------------------------------- corners

        private void DrawKillFeed(float w, MatchManager match)
        {
            if (match == null) return;
            var feed = match.KillFeed;
            float y = Margin;
            int shown = 0;
            for (int i = feed.Count - 1; i >= 0 && shown < 5; i--)
            {
                var k = feed[i];
                float age = Time.time - k.Time;
                if (age > FeedDuration) break;
                float alpha = Mathf.Clamp01(FeedDuration - age);

                string killer = k.Killer != null ? k.Killer.displayName : "";
                string weapon = k.Killer == null ? "oops" : (k.WeaponId == "sniper" ? "ZAPPED" : "PEWED") + (k.Headshot ? " +HS" : "");
                string victim = k.Victim.displayName;
                float kw = killer.Length > 0 ? _label.CalcSize(new GUIContent(killer)).x : 0f;
                float ww = _small.CalcSize(new GUIContent(weapon)).x;
                float vw = _label.CalcSize(new GUIContent(victim)).x;
                float total = kw + ww + vw + (killer.Length > 0 ? 36f : 24f);

                var row = new Rect(w - Margin - total, y, total, 26f);
                HudSkin.RoundedRect(row, new Color(HudSkin.Panel.r, HudSkin.Panel.g, HudSkin.Panel.b, HudSkin.Panel.a * alpha));
                float x = row.x + 12f;
                if (killer.Length > 0)
                {
                    HudSkin.Label(new Rect(x, y, kw, 26f), killer, _label, WithAlpha(k.Killer.color, alpha), TextAnchor.MiddleLeft);
                    x += kw + 6f;
                }
                HudSkin.Label(new Rect(x, y, ww, 26f), weapon, _small, WithAlpha(k.Headshot ? HeadColor : HudSkin.Dim, alpha), TextAnchor.MiddleLeft);
                x += ww + 6f;
                HudSkin.Label(new Rect(x, y, vw, 26f), victim, _label, WithAlpha(k.Victim.color, alpha), TextAnchor.MiddleLeft);
                y += 30f;
                shown++;
            }
        }

        private void DrawScoreWidget(float w, float h, MatchManager match)
        {
            if (match == null || match.Score == null) return;
            var ranked = match.Score.Ranked();
            if (ranked.Count == 0) return;

            var me = match.Score.Get(combatant.Id);
            var rival = ranked[0].Id == combatant.Id ? (ranked.Count > 1 ? ranked[1] : null) : ranked[0];

            var panel = new Rect(w - Margin - 230f, h - Margin - 86f, 230f, 86f);
            HudSkin.RoundedRect(panel, HudSkin.Panel);
            HudSkin.Label(new Rect(panel.x + 14f, panel.y + 6f, 150f, 18f), $"FIRST TO {match.Score.ScoreLimit}", _small, HudSkin.Dim, TextAnchor.MiddleLeft);
            if (match.HasTimeLimit)
            {
                bool lastMinute = match.TimeRemaining < 60f && !match.IsOver;
                HudSkin.Label(new Rect(panel.xMax - 90f, panel.y + 4f, 76f, 22f), FormatTime(match.TimeRemaining), _label,
                    lastMinute ? HudSkin.HealthMid : HudSkin.Text, TextAnchor.MiddleRight);
            }
            ScoreRow(new Rect(panel.x + 14f, panel.y + 26f, 202f, 26f), "YOU", combatant.color, me != null ? me.Score : 0, match.Score.ScoreLimit, true);
            if (rival != null)
            {
                var rivalCombatant = FindCombatant(rival.Id);
                string label = ranked[0].Id == combatant.Id ? "2ND  " + rival.Name : "LEAD  " + rival.Name;
                ScoreRow(new Rect(panel.x + 14f, panel.y + 54f, 202f, 26f), label, rivalCombatant != null ? rivalCombatant.color : HudSkin.Text, rival.Score, match.Score.ScoreLimit, false);
            }
        }

        private void ScoreRow(Rect r, string label, Color color, int score, int limit, bool mine)
        {
            HudSkin.DrawTexture(new Rect(r.x, r.y + 8f, 10f, 10f), HudSkin.Circle, color);
            HudSkin.Label(new Rect(r.x + 16f, r.y, 120f, r.height), label, mine ? _label : _small, mine ? HudSkin.Text : HudSkin.Dim, TextAnchor.MiddleLeft);
            var bar = new Rect(r.x + 110f, r.y + 10f, 60f, 6f);
            HudSkin.Fill(bar, new Color(1f, 1f, 1f, 0.12f));
            HudSkin.Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01((float)score / limit), bar.height), color);
            HudSkin.Label(new Rect(r.xMax - 30f, r.y, 30f, r.height), score.ToString(), _label, HudSkin.Text, TextAnchor.MiddleRight);
        }

        /// <summary>Halo-style: the name of whoever is under your crosshair.</summary>
        private void DrawAimName(Vector2 c)
        {
            var target = weapons.AimTarget;
            if (target == null || weapons.IsZoomed) return;
            HudSkin.Label(new Rect(c.x - 150f, c.y + 22f, 300f, 22f), target.displayName, _small, target.color, TextAnchor.MiddleCenter);
        }

        private void DrawOnlineBadge(float w)
        {
            if (!NetSession.IsOnline) return;
            var session = NetSession.Current;
            string text = (session.Role == NetRole.Host ? "HOSTING" : "ONLINE") + $"  ·  {session.PlayerCount} PLAYER" + (session.PlayerCount == 1 ? "" : "S");
            var size = _small.CalcSize(new GUIContent(text));
            var r = new Rect((w - size.x) / 2f - 12f, 40f, size.x + 24f, 22f);
            HudSkin.RoundedRect(r, HudSkin.Panel);
            HudSkin.Label(r, text, _small, HudSkin.Accent, TextAnchor.MiddleCenter, shadow: false);
        }

        private void DrawSniperTimer(float w)
        {
            if (sniperPad == null) return;
            string text = sniperPad.HasWeaponOnPad
                ? "LONG ZAPPER ON PAD"
                : $"LONG ZAPPER IN {FormatTime(sniperPad.TimeUntilNextSpawn)}";
            var size = _small.CalcSize(new GUIContent(text));
            var r = new Rect((w - size.x) / 2f - 12f, 12f, size.x + 24f, 24f);
            HudSkin.RoundedRect(r, HudSkin.Panel);
            HudSkin.Label(r, text, _small, sniperPad.HasWeaponOnPad ? OutdoorPalette.SniperPad : HudSkin.Text, TextAnchor.MiddleCenter, shadow: false);
        }

        private void DrawHelp(bool gamepad)
        {
            string text = gamepad
                ? "LS move   RS look   A jump   RT fire   LT/RS-click scope\n" +
                  "X reload (hold X: pick up)   Y switch   View: scores   Menu: pause"
                : "WASD move   Space jump   Mouse aim   LMB fire   RMB scope\n" +
                  "R reload   E pick up   Q/wheel switch   Tab scores   Esc pause\n" +
                  "F1 hide this   K hurt yourself (test regen)";
            var r = new Rect(Margin, Margin, 470f, gamepad ? 64f : 80f);
            HudSkin.RoundedRect(r, HudSkin.Panel);
            HudSkin.Label(new Rect(r.x + 12f, r.y + 6f, r.width - 24f, 18f), mapName, _small, HudSkin.Accent, TextAnchor.UpperLeft, shadow: false);
            HudSkin.Label(new Rect(r.x + 12f, r.y + 24f, r.width - 24f, r.height - 28f), text, _small, HudSkin.Dim, TextAnchor.UpperLeft, shadow: false);
        }

        // ---------------------------------------------------------------- scoreboard / results

        private void DrawScoreboard(float w, float top, MatchManager match)
        {
            var ranked = match.Score.Ranked();
            float rowH = 30f;
            var panel = new Rect(w / 2f - 250f, top, 500f, 64f + rowH * ranked.Count);
            HudSkin.RoundedRect(panel, new Color(0.03f, 0.05f, 0.08f, 0.85f));
            HudSkin.Label(new Rect(panel.x + 20f, panel.y + 10f, 300f, 24f), "FREE FOR ALL", _label, HudSkin.Accent, TextAnchor.MiddleLeft);
            string rules = $"FIRST TO {match.Score.ScoreLimit}" + (match.HasTimeLimit ? $"  ·  {FormatTime(match.TimeRemaining)} LEFT" : "");
            HudSkin.Label(new Rect(panel.xMax - 260f, panel.y + 10f, 240f, 24f), rules, _small, HudSkin.Dim, TextAnchor.MiddleRight);

            float y = panel.y + 40f;
            float colK = panel.xMax - 170f, colD = panel.xMax - 110f, colS = panel.xMax - 50f;
            HudSkin.Label(new Rect(colK, y, 40f, 18f), "K", _small, HudSkin.Dim, TextAnchor.MiddleCenter);
            HudSkin.Label(new Rect(colD, y, 40f, 18f), "D", _small, HudSkin.Dim, TextAnchor.MiddleCenter);
            HudSkin.Label(new Rect(colS, y, 40f, 18f), "SCORE", _small, HudSkin.Dim, TextAnchor.MiddleCenter);
            y += 20f;

            for (int i = 0; i < ranked.Count; i++)
            {
                var e = ranked[i];
                var c = FindCombatant(e.Id);
                bool mine = e.Id == combatant.Id;
                var row = new Rect(panel.x + 10f, y, panel.width - 20f, rowH - 4f);
                if (mine) HudSkin.RoundedRect(row, new Color(1f, 1f, 1f, 0.1f));
                HudSkin.Label(new Rect(row.x + 10f, y, 24f, row.height), (i + 1).ToString(), _label, HudSkin.Dim, TextAnchor.MiddleLeft);
                HudSkin.DrawTexture(new Rect(row.x + 38f, y + 8f, 10f, 10f), HudSkin.Circle, c != null ? c.color : Color.white);
                HudSkin.Label(new Rect(row.x + 56f, y, 200f, row.height), e.Name, _label, HudSkin.Text, TextAnchor.MiddleLeft);
                HudSkin.Label(new Rect(colK, y, 40f, row.height), e.Kills.ToString(), _label, HudSkin.Text, TextAnchor.MiddleCenter);
                HudSkin.Label(new Rect(colD, y, 40f, row.height), e.Deaths.ToString(), _label, HudSkin.Text, TextAnchor.MiddleCenter);
                HudSkin.Label(new Rect(colS, y, 40f, row.height), e.Score.ToString(), _label, HudSkin.Accent, TextAnchor.MiddleCenter);
                y += rowH;
            }
        }

        private void DrawResults(float w, float h, MatchManager match)
        {
            var winner = match.Score.Winner;
            string title;
            Color titleColor = Color.white;
            if (winner == null)
            {
                title = "DRAW";
            }
            else
            {
                var winnerCombatant = FindCombatant(winner.Id);
                title = winner.Id == combatant.Id ? "YOU WIN!" : winner.Name.ToUpperInvariant() + " WINS";
                if (winnerCombatant != null) titleColor = winnerCombatant.color;
            }
            HudSkin.Label(new Rect(0, h * 0.1f, w, 50f), title, _title, titleColor, TextAnchor.MiddleCenter);
            HudSkin.Label(new Rect(0, h * 0.1f + 48f, w, 24f), $"Next match in {Mathf.CeilToInt(match.RestartCountdown)}s", _label, HudSkin.Dim, TextAnchor.MiddleCenter);
            DrawScoreboard(w, h * 0.1f + 84f, match);
        }

        // ---------------------------------------------------------------- helpers

        private static Combatant FindCombatant(int id)
        {
            foreach (var c in Combatant.All)
                if (c.Id == id) return c;
            return null;
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

        private static string FormatTime(float seconds)
        {
            int s = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            _label.normal.textColor = Color.white;
            _small = new GUIStyle(_label) { fontSize = 13, fontStyle = FontStyle.Bold };
            _medium = new GUIStyle(_label) { fontSize = 20 };
            _big = new GUIStyle(_label) { fontSize = 40 };
            _title = new GUIStyle(_label) { fontSize = 44 };
        }
    }
}
