using System;
using System.Collections.Generic;
using ArenaShooter.Core;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Main menu, match setup, settings, controls and pause menu. Drawn with IMGUI + HudSkin and
    /// fully usable with mouse, keyboard or controller (D-pad/stick to move, A select, B back,
    /// left/right to change values).
    /// </summary>
    public class MenuUI : MonoBehaviour
    {
        public ArenaBootstrap flow;

        public bool IsOpen => _stack.Count > 0;

        // ------------------------------------------------------------ item model

        private abstract class Item
        {
            public string Label;
            public string Hint;
            public Func<bool> Enabled = () => true;
            public virtual string Value => null;
            public abstract void Activate(int direction);
        }

        private sealed class ButtonItem : Item
        {
            public Action OnPress;
            public override void Activate(int direction)
            {
                if (direction >= 0) OnPress?.Invoke();
            }
        }

        private sealed class ChoiceItem : Item
        {
            public string[] Choices;
            public Func<int> Get;
            public Action<int> Set;
            public override string Value => Choices[Mathf.Clamp(Get(), 0, Choices.Length - 1)];
            public override void Activate(int direction)
            {
                int d = direction == 0 ? 1 : direction;
                Set((Get() + d + Choices.Length) % Choices.Length);
            }
        }

        private sealed class SliderItem : Item
        {
            public float Min, Max, Step;
            public Func<float> Get;
            public Action<float> Set;
            public Func<float, string> Format;
            public override string Value => Format(Get());
            public override void Activate(int direction)
            {
                if (direction == 0) return;
                float v = Mathf.Clamp(Mathf.Round((Get() + Step * direction) / Step) * Step, Min, Max);
                Set(v);
            }
        }

        private sealed class Page
        {
            public string Title;
            public string Body; // optional text block (controls page)
            public readonly List<Item> Items = new List<Item>();
            public int Focus;
            public bool IsPauseRoot;
        }

        private readonly List<Page> _stack = new List<Page>();
        private GUIStyle _title, _subtitle, _item, _value, _hint, _body;
        private Vector2 _lastMouse;
        // Input is ignored on the frame a page opens, so the Esc/A that opened it doesn't also act on it.
        private int _openedFrame = -1;
        private MatchSetup _setup;

        private Page Current => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;

        // ------------------------------------------------------------ opening pages

        public void OpenMain()
        {
            _openedFrame = Time.frameCount;
            _stack.Clear();
            _stack.Add(MainPage());
        }

        public void OpenPause()
        {
            _openedFrame = Time.frameCount;
            _stack.Clear();
            _stack.Add(PausePage());
        }

        public void Close() => _stack.Clear();

        private void Push(Page page)
        {
            _openedFrame = Time.frameCount;
            _stack.Add(page);
        }

        private void Back()
        {
            var page = Current;
            if (page == null) return;
            if (page.IsPauseRoot)
            {
                flow.Resume();
                return;
            }
            if (_stack.Count > 1) _stack.RemoveAt(_stack.Count - 1);
        }

        private Page MainPage()
        {
            var p = new Page { Title = "SPACE GRUNTS" };
            p.Items.Add(Button("Play vs Bots", "Free-for-All against bots.", () => Push(SetupPage())));
            p.Items.Add(Button("Multiplayer", "Play with friends online.", () => Push(MultiplayerPage())));
            p.Items.Add(Button("Settings", "Sensitivity, controller, field of view, volume.", () => Push(SettingsPage())));
            p.Items.Add(Button("Controls", "Keyboard + mouse and Xbox controller layouts.", () => Push(ControlsPage())));
            p.Items.Add(Button("Quit", "Close the game.", Quit));
            return p;
        }

        private Page SetupPage()
        {
            _setup = GameSettings.LastSetup.Clone();
            var p = new Page { Title = "PLAY VS BOTS" };
            p.Items.Add(Choice("Map", new[] { "Crash Site", "Test Range" }, () => (int)_setup.map, v => _setup.map = (MapChoice)v,
                "Crash Site: two dropships, one alien planet. Test Range: target dummies, no bots."));
            var bots = new string[MatchSetup.MaxBots + 1];
            for (int i = 0; i < bots.Length; i++) bots[i] = i.ToString();
            var botItem = Choice("Bots", bots, () => _setup.botCount, v => _setup.botCount = v, "How many bots join you (you + 5 = 6 players).");
            botItem.Enabled = () => _setup.map != MapChoice.TestRange;
            p.Items.Add(botItem);
            var diff = Choice("Bot difficulty", new[] { "Easy", "Normal", "Hard" }, () => (int)_setup.difficulty, v => _setup.difficulty = (Difficulty)v,
                "How fast bots react and how well they aim.");
            diff.Enabled = botItem.Enabled;
            p.Items.Add(diff);
            p.Items.Add(Choice("Score limit", Labels(MatchSetup.ScoreLimitChoices, n => n + " kills"),
                () => IndexOf(MatchSetup.ScoreLimitChoices, _setup.scoreLimit, 2), v => _setup.scoreLimit = MatchSetup.ScoreLimitChoices[v],
                "First to this many kills wins."));
            p.Items.Add(Choice("Time limit", Labels(MatchSetup.TimeLimitChoices, n => n == 0 ? "None" : n + " min"),
                () => IndexOf(MatchSetup.TimeLimitChoices, _setup.timeLimitMinutes, 1), v => _setup.timeLimitMinutes = MatchSetup.TimeLimitChoices[v],
                "When time runs out, the leader wins (a tie is a draw)."));
            p.Items.Add(Button("Start Match", "Drop in!", () => flow.StartMatch(_setup)));
            p.Items.Add(Button("Back", null, Back));
            return p;
        }

        private Page MultiplayerPage()
        {
            var p = new Page
            {
                Title = "MULTIPLAYER",
                Body = "Online play is the next big step.\nHost a game, send your friend a join code, and fight together.",
            };
            var host = Button("Host Game", "Coming soon: host a match and get a join code for your friend.", null);
            host.Enabled = () => false;
            var join = Button("Join Game", "Coming soon: enter a friend's join code.", null);
            join.Enabled = () => false;
            p.Items.Add(host);
            p.Items.Add(join);
            p.Items.Add(Button("Back", null, Back));
            return p;
        }

        private Page SettingsPage()
        {
            var p = new Page { Title = "SETTINGS" };
            p.Items.Add(Slider("Mouse sensitivity", 0.2f, 3f, 0.1f, () => GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v, v => v.ToString("0.0") + "x", null));
            p.Items.Add(Slider("Controller look speed", 0.3f, 2f, 0.1f, () => GameSettings.StickSensitivity, v => GameSettings.StickSensitivity = v, v => v.ToString("0.0") + "x", null));
            p.Items.Add(Toggle("Invert look (Y)", () => GameSettings.InvertY, v => GameSettings.InvertY = v, null));
            p.Items.Add(Toggle("Aim assist (controller)", () => GameSettings.AimAssist, v => GameSettings.AimAssist = v, "Aim slows down while your crosshair is on an enemy."));
            p.Items.Add(Toggle("Vibration", () => GameSettings.Rumble, v => GameSettings.Rumble = v, null));
            p.Items.Add(Slider("Field of view", 55f, 80f, 1f, () => GameSettings.FieldOfView, v => GameSettings.FieldOfView = v, v => v.ToString("0") + "°", "Vertical field of view."));
            p.Items.Add(Slider("Volume", 0f, 1f, 0.05f, () => GameSettings.Volume, v => GameSettings.Volume = v, v => Mathf.RoundToInt(v * 100f) + "%", null));
            p.Items.Add(Toggle("Show controls hint", () => GameSettings.ShowControlsHint, v => GameSettings.ShowControlsHint = v, "The small controls panel in the top-left during matches."));
            p.Items.Add(Button("Back", null, Back));
            return p;
        }

        private Page ControlsPage()
        {
            var p = new Page
            {
                Title = "CONTROLS",
                Body =
                    "ACTION              KEYBOARD + MOUSE        XBOX CONTROLLER\n" +
                    "Move / look         WASD / mouse            Left stick / right stick\n" +
                    "Jump                Space                   A\n" +
                    "Fire                Left mouse              RT\n" +
                    "Scope (Long Zapper) Right mouse             LT or click right stick\n" +
                    "Reload              R                       X\n" +
                    "Pick up weapon      E                       Hold X\n" +
                    "Switch weapon       Q / wheel / 1 / 2       Y\n" +
                    "Scoreboard          Tab (hold)              View (hold)\n" +
                    "Pause               Esc                     Menu",
            };
            p.Items.Add(Button("Back", null, Back));
            return p;
        }

        private Page PausePage()
        {
            var p = new Page { Title = "PAUSED", IsPauseRoot = true };
            p.Items.Add(Button("Resume", null, () => flow.Resume()));
            p.Items.Add(Button("Settings", null, () => Push(SettingsPage())));
            p.Items.Add(Button("Controls", null, () => Push(ControlsPage())));
            p.Items.Add(Button("Quit to Main Menu", "Leaves the current match.", () => flow.ShowMainMenu()));
            return p;
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------ item helpers

        private static ButtonItem Button(string label, string hint, Action onPress) =>
            new ButtonItem { Label = label, Hint = hint, OnPress = onPress };

        private static ChoiceItem Choice(string label, string[] choices, Func<int> get, Action<int> set, string hint) =>
            new ChoiceItem { Label = label, Choices = choices, Get = get, Set = set, Hint = hint };

        private static ChoiceItem Toggle(string label, Func<bool> get, Action<bool> set, string hint) =>
            new ChoiceItem
            {
                Label = label, Hint = hint, Choices = new[] { "Off", "On" },
                Get = () => get() ? 1 : 0,
                Set = v => { set(v == 1); GameSettings.Save(); },
            };

        private static SliderItem Slider(string label, float min, float max, float step, Func<float> get, Action<float> set, Func<float, string> format, string hint) =>
            new SliderItem
            {
                Label = label, Hint = hint, Min = min, Max = max, Step = step, Get = get, Format = format,
                Set = v => { set(v); GameSettings.Save(); },
            };

        private static string[] Labels(int[] values, Func<int, string> format)
        {
            var labels = new string[values.Length];
            for (int i = 0; i < values.Length; i++) labels[i] = format(values[i]);
            return labels;
        }

        private static int IndexOf(int[] values, int value, int fallback)
        {
            int i = Array.IndexOf(values, value);
            return i >= 0 ? i : fallback;
        }

        // ------------------------------------------------------------ input

        private void Update()
        {
            var page = Current;
            if (page == null || Time.frameCount == _openedFrame) return;

            var cmd = MenuInput.Read();
            if (cmd.Back)
            {
                Back();
                return;
            }
            if (page.Items.Count == 0) return;

            if (cmd.Vertical != 0) page.Focus = (page.Focus + cmd.Vertical + page.Items.Count) % page.Items.Count;
            var item = page.Items[page.Focus];
            if (!item.Enabled()) return;
            if (cmd.Horizontal != 0 && !(item is ButtonItem)) item.Activate(cmd.Horizontal);
            if (cmd.Confirm) item.Activate(0);
        }

        // ------------------------------------------------------------ drawing

        private void OnGUI()
        {
            var page = Current;
            if (page == null) return;
            EnsureStyles();
            GUI.depth = -10; // above the HUD

            float w = Screen.width, h = Screen.height;
            bool pause = page.IsPauseRoot || _stack[0].IsPauseRoot;
            HudSkin.Fill(new Rect(0, 0, w, h), new Color(0.02f, 0.03f, 0.06f, pause ? 0.55f : 0.25f));
            HudSkin.DrawTexture(new Rect(-w * 0.6f, -h * 0.5f, w * 1.3f, h * 2f), HudSkin.Circle, new Color(0.02f, 0.04f, 0.08f, 0.55f));

            float x = Mathf.Max(48f, w * 0.07f);
            float y = h * 0.14f;
            HudSkin.Label(new Rect(x, y, 800f, 64f), page.Title, _title, Color.white, TextAnchor.MiddleLeft);
            y += 64f;
            if (_stack.Count == 1 && !pause)
            {
                HudSkin.Label(new Rect(x + 4f, y, 800f, 24f), "goofy space marines  ·  prototype build", _subtitle, HudSkin.Accent, TextAnchor.MiddleLeft);
            }
            y += 40f;

            if (!string.IsNullOrEmpty(page.Body))
            {
                var bodySize = _body.CalcSize(new GUIContent(page.Body));
                var bodyRect = new Rect(x, y, Mathf.Max(460f, bodySize.x + 32f), bodySize.y + 24f);
                HudSkin.RoundedRect(bodyRect, HudSkin.Panel);
                HudSkin.Label(new Rect(bodyRect.x + 16f, bodyRect.y + 12f, bodySize.x + 4f, bodySize.y), page.Body, _body, HudSkin.Text, TextAnchor.UpperLeft, shadow: false);
                y = bodyRect.yMax + 20f;
            }

            Event e = Event.current;
            bool mouseMoved = (e.mousePosition - _lastMouse).sqrMagnitude > 1f;
            _lastMouse = e.mousePosition;

            const float itemW = 460f, itemH = 46f;
            for (int i = 0; i < page.Items.Count; i++)
            {
                var item = page.Items[i];
                var r = new Rect(x, y + i * (itemH + 6f), itemW, itemH);
                bool enabled = item.Enabled();
                bool focused = i == page.Focus;

                if (r.Contains(e.mousePosition))
                {
                    if (mouseMoved)
                    {
                        page.Focus = i;
                        MenuInput.NoteMouseUsed();
                    }
                    if (e.type == EventType.MouseDown && enabled)
                    {
                        page.Focus = i;
                        MenuInput.NoteMouseUsed();
                        item.Activate(e.button == 1 ? -1 : e.button == 0 && item is SliderItem ? 1 : 0);
                        e.Use();
                        return; // the page may have changed
                    }
                }

                HudSkin.RoundedRect(r, focused ? new Color(1f, 1f, 1f, 0.16f) : HudSkin.Panel);
                if (focused) HudSkin.Fill(new Rect(r.x, r.y + 8f, 4f, r.height - 16f), HudSkin.Accent);
                Color textColor = !enabled ? new Color(1f, 1f, 1f, 0.3f) : focused ? Color.white : new Color(1f, 1f, 1f, 0.8f);
                HudSkin.Label(new Rect(r.x + 20f, r.y, 260f, r.height), item.Label, _item, textColor, TextAnchor.MiddleLeft);
                if (item.Value != null)
                {
                    string value = enabled ? (focused ? "‹  " + item.Value + "  ›" : item.Value) : "—";
                    HudSkin.Label(new Rect(r.xMax - 200f, r.y, 184f, r.height), value, _value, focused ? HudSkin.Accent : textColor, TextAnchor.MiddleRight);
                }
            }

            float listBottom = y + page.Items.Count * (itemH + 6f);
            var focusedItem = page.Items.Count > 0 ? page.Items[page.Focus] : null;
            if (focusedItem?.Hint != null)
                HudSkin.Label(new Rect(x + 4f, listBottom + 6f, 700f, 24f), focusedItem.Hint, _hint, HudSkin.Dim, TextAnchor.MiddleLeft);

            bool gamepad = MenuInput.LastDevice == InputDeviceKind.Gamepad;
            string footer = gamepad ? "A  select      B  back      ◀ ▶  change" : "Enter / click  select      Esc  back      ◀ ▶  change";
            HudSkin.Label(new Rect(x, h - 56f, 700f, 24f), footer, _hint, HudSkin.Dim, TextAnchor.MiddleLeft);
        }

        private void EnsureStyles()
        {
            if (_item != null) return;
            _item = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            _item.normal.textColor = Color.white;
            _title = new GUIStyle(_item) { fontSize = 52 };
            _subtitle = new GUIStyle(_item) { fontSize = 15 };
            _value = new GUIStyle(_item) { fontSize = 18 };
            _hint = new GUIStyle(_item) { fontSize = 14, fontStyle = FontStyle.Normal };
            _body = new GUIStyle(_hint) { fontSize = 14, wordWrap = false };
            var mono = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Menlo", "Courier New", "DejaVu Sans Mono" }, 14);
            if (mono != null) _body.font = mono;
        }
    }
}
