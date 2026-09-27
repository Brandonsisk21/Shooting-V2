using ArenaShooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArenaShooter.Gameplay
{
    public enum InputDeviceKind
    {
        KeyboardMouse,
        Gamepad,
    }

    /// <summary>One frame of player intent, independent of which device produced it.</summary>
    public struct PlayerCommands
    {
        public Vector2 Move;
        /// <summary>Look change this frame in degrees (x = yaw, y = pitch up), before zoom scaling.</summary>
        public Vector2 LookDegrees;
        public bool Jump;
        public bool Fire;
        public bool ToggleZoom;
        public bool Reload;
        public bool Pickup;
        public bool SwitchWeapon;
        /// <summary>Slot to switch to, or -1.</summary>
        public int SwitchToSlot;
        public bool ScoreboardHeld;
        public bool ToggleHelp;
        public bool DebugSelfDamage;
    }

    /// <summary>
    /// Reads keyboard + mouse and an Xbox (or any) gamepad through the Input System package and
    /// merges them into <see cref="PlayerCommands"/>. Either device works at any time; HUD prompts
    /// follow whichever was used last.
    ///
    /// Gamepad layout (Halo-style): LS move, RS look, A jump, RT fire, LT or RS-click scope,
    /// X reload (hold X to pick up a weapon), Y switch weapon, View hold = scoreboard, Menu = help.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("Mouse")]
        public float mouseDegreesPerPixel = 0.12f;

        [Header("Gamepad look")]
        [Tooltip("Max yaw speed at full stick tilt, degrees per second.")]
        public float stickYawSpeed = 200f;
        [Tooltip("Max pitch speed at full stick tilt, degrees per second.")]
        public float stickPitchSpeed = 130f;
        [Tooltip("Extra turn speed multiplier reached after holding the stick fully sideways.")]
        public float stickTurnBoost = 1.5f;
        public float stickTurnBoostDelay = 0.3f;
        public float stickTurnBoostRampTime = 0.4f;
        public float lookDeadzone = 0.15f;
        [Tooltip("Higher = finer control near center, same top speed.")]
        public float lookExponent = 2f;
        public float moveDeadzone = 0.15f;
        public bool invertLookY;
        [Tooltip("Look speed multiplier while the crosshair is over an enemy (gamepad aim assist friction). 1 = off.")]
        [Range(0.2f, 1f)] public float aimAssistFriction = 0.55f;

        [Header("Gamepad buttons")]
        [Tooltip("Hold X this long to pick up a weapon; a shorter tap reloads.")]
        public float pickupHoldTime = 0.3f;
        public bool rumble = true;

        public InputDeviceKind ActiveDevice { get; private set; } = InputDeviceKind.KeyboardMouse;
        public bool HasGamepad => Gamepad.current != null;

        private float _fullTiltTime;
        private float _xHeldTime = -1f;
        private bool _xConsumed;
        private float _rumbleUntil;

        /// <param name="pickupAvailable">Whether a pickup prompt is showing (decides X tap vs. hold on the gamepad).</param>
        /// <param name="onEnemy">Crosshair is over an enemy (aim assist friction).</param>
        /// <param name="acceptMouse">Mouse look/buttons only count while the cursor is captured.</param>
        public PlayerCommands Read(float deltaTime, bool pickupAvailable, bool onEnemy, bool acceptMouse)
        {
            var cmd = new PlayerCommands { SwitchToSlot = -1 };
            ReadKeyboardMouse(ref cmd, acceptMouse);
            ReadGamepad(ref cmd, deltaTime, pickupAvailable, onEnemy);
            cmd.Move = Vector2.ClampMagnitude(cmd.Move, 1f);
            return cmd;
        }

        /// <summary>Brief controller vibration (no-op without a gamepad or with rumble off).</summary>
        public void Rumble(float low, float high, float duration)
        {
            var pad = Gamepad.current;
            if (!rumble || pad == null || ActiveDevice != InputDeviceKind.Gamepad) return;
            pad.SetMotorSpeeds(low, high);
            _rumbleUntil = Mathf.Max(_rumbleUntil, Time.unscaledTime + duration);
        }

        private void Update()
        {
            if (_rumbleUntil > 0f && Time.unscaledTime >= _rumbleUntil)
            {
                _rumbleUntil = 0f;
                Gamepad.current?.ResetHaptics();
            }
        }

        private void OnDisable() => Gamepad.current?.ResetHaptics();

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Gamepad.current?.ResetHaptics();
        }

        private void ReadKeyboardMouse(ref PlayerCommands cmd, bool acceptMouse)
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            if (kb != null)
            {
                if (kb.anyKey.wasPressedThisFrame) ActiveDevice = InputDeviceKind.KeyboardMouse;

                float x = (Held(kb, Key.D) || Held(kb, Key.RightArrow) ? 1f : 0f) - (Held(kb, Key.A) || Held(kb, Key.LeftArrow) ? 1f : 0f);
                float y = (Held(kb, Key.W) || Held(kb, Key.UpArrow) ? 1f : 0f) - (Held(kb, Key.S) || Held(kb, Key.DownArrow) ? 1f : 0f);
                cmd.Move += new Vector2(x, y);
                cmd.Jump |= Pressed(kb, Key.Space);
                cmd.Reload |= Pressed(kb, Key.R);
                cmd.Pickup |= Pressed(kb, Key.E);
                cmd.SwitchWeapon |= Pressed(kb, Key.Q);
                if (Pressed(kb, Key.Digit1)) cmd.SwitchToSlot = 0;
                if (Pressed(kb, Key.Digit2)) cmd.SwitchToSlot = 1;
                cmd.ScoreboardHeld |= Held(kb, Key.Tab);
                cmd.ToggleHelp |= Pressed(kb, Key.F1);
                cmd.DebugSelfDamage |= Pressed(kb, Key.K);
            }

            if (mouse != null && acceptMouse)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (delta.sqrMagnitude > 1f) ActiveDevice = InputDeviceKind.KeyboardMouse;
                cmd.LookDegrees += delta * mouseDegreesPerPixel;
                cmd.Fire |= mouse.leftButton.wasPressedThisFrame;
                cmd.ToggleZoom |= mouse.rightButton.wasPressedThisFrame;
                cmd.SwitchWeapon |= Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f;
            }
        }

        private void ReadGamepad(ref PlayerCommands cmd, float deltaTime, bool pickupAvailable, bool onEnemy)
        {
            var pad = Gamepad.current;
            if (pad == null) return;

            Vector2 moveRaw = pad.leftStick.ReadValue();
            Vector2 lookRaw = pad.rightStick.ReadValue();
            bool anyButton = pad.buttonSouth.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame ||
                             pad.buttonWest.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame ||
                             pad.rightTrigger.wasPressedThisFrame || pad.leftTrigger.wasPressedThisFrame;
            if (anyButton || moveRaw.magnitude > 0.3f || lookRaw.magnitude > 0.3f) ActiveDevice = InputDeviceKind.Gamepad;

            StickCurve.Apply(moveRaw.x, moveRaw.y, moveDeadzone, 1f, out float mx, out float my);
            cmd.Move += new Vector2(mx, my);

            StickCurve.Apply(lookRaw.x, lookRaw.y, lookDeadzone, lookExponent, out float lx, out float ly);
            // Halo-style turn boost: holding the stick fully sideways ramps turn speed up.
            _fullTiltTime = Mathf.Abs(lookRaw.x) > 0.95f ? _fullTiltTime + deltaTime : 0f;
            float boost = 1f + (stickTurnBoost - 1f) * Mathf.Clamp01((_fullTiltTime - stickTurnBoostDelay) / Mathf.Max(0.01f, stickTurnBoostRampTime));
            float friction = onEnemy ? aimAssistFriction : 1f;
            float pitchSign = invertLookY ? -1f : 1f;
            cmd.LookDegrees += new Vector2(lx * stickYawSpeed * boost, ly * stickPitchSpeed * pitchSign) * (friction * deltaTime);

            cmd.Jump |= pad.buttonSouth.wasPressedThisFrame;
            cmd.Fire |= pad.rightTrigger.wasPressedThisFrame;
            cmd.ToggleZoom |= pad.leftTrigger.wasPressedThisFrame || pad.rightStickButton.wasPressedThisFrame;
            cmd.SwitchWeapon |= pad.buttonNorth.wasPressedThisFrame;
            cmd.ScoreboardHeld |= pad.selectButton.isPressed;
            cmd.ToggleHelp |= pad.startButton.wasPressedThisFrame;
            ReadReloadOrPickup(pad, ref cmd, deltaTime, pickupAvailable);
        }

        /// <summary>X: tap to reload; with a pickup prompt showing, hold to pick up (Halo 2 style).</summary>
        private void ReadReloadOrPickup(Gamepad pad, ref PlayerCommands cmd, float deltaTime, bool pickupAvailable)
        {
            var x = pad.buttonWest;
            if (x.wasPressedThisFrame)
            {
                _xHeldTime = 0f;
                _xConsumed = false;
                if (!pickupAvailable)
                {
                    cmd.Reload = true;
                    _xConsumed = true;
                }
            }
            else if (x.isPressed && _xHeldTime >= 0f)
            {
                _xHeldTime += deltaTime;
                if (!_xConsumed && pickupAvailable && _xHeldTime >= pickupHoldTime)
                {
                    cmd.Pickup = true;
                    _xConsumed = true;
                }
            }

            if (x.wasReleasedThisFrame)
            {
                if (!_xConsumed) cmd.Reload = true;
                _xHeldTime = -1f;
            }
        }

        private static bool Held(Keyboard kb, Key key) => kb[key].isPressed;
        private static bool Pressed(Keyboard kb, Key key) => kb[key].wasPressedThisFrame;
    }
}
