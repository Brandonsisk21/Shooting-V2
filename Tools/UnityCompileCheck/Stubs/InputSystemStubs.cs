// Compile-check stand-ins for the parts of com.unity.inputsystem the game uses. Signatures are
// copied from the real package (Unity-Technologies/InputSystem, Runtime/Devices + Runtime/Controls)
// so a pass here means the calls match the real API. NOT shipped: Unity uses the real package.
// If game code starts using more of the Input System, mirror the exact signatures here.
#pragma warning disable CS0067, CS8618
namespace UnityEngine.InputSystem.Controls
{
    public abstract class InputControl<TValue> where TValue : struct
    {
        public TValue ReadValue() => default;
    }

    public class AxisControl : InputControl<float> { }

    public class ButtonControl : AxisControl
    {
        public float pressPoint = -1;
        public bool isPressed => false;
        public bool wasPressedThisFrame => false;
        public bool wasReleasedThisFrame => false;
    }

    public class KeyControl : ButtonControl { }
    public class AnyKeyControl : ButtonControl { }
    public class Vector2Control : InputControl<Vector2> { }
    public class StickControl : Vector2Control { }
    public class DeltaControl : Vector2Control { }

    public class DpadControl : Vector2Control
    {
        public ButtonControl up { get; set; }
        public ButtonControl down { get; set; }
        public ButtonControl left { get; set; }
        public ButtonControl right { get; set; }
    }
}

namespace UnityEngine.InputSystem
{
    using UnityEngine.InputSystem.Controls;

    public enum Key
    {
        None, Space, Tab, A, D, E, K, Q, R, S, W, Digit1, Digit2, Escape,
        LeftArrow, RightArrow, UpArrow, DownArrow, F1, F10, Enter, Backspace, NumpadEnter, C, F, G, V, LeftCtrl,
    }

    public class InputDevice { }

    public class Keyboard : InputDevice
    {
        public static Keyboard current { get; private set; }
        public AnyKeyControl anyKey { get; protected set; }
        public KeyControl this[Key key] => null;
        public KeyControl escapeKey => this[Key.Escape];
    }

    public class Pointer : InputDevice
    {
        public static Pointer current { get; internal set; }
        public DeltaControl delta { get; protected set; }
    }

    public class Mouse : Pointer
    {
        public new static Mouse current { get; private set; }
        public DeltaControl scroll { get; protected set; }
        public ButtonControl leftButton { get; protected set; }
        public ButtonControl rightButton { get; protected set; }
    }

    public class Gamepad : InputDevice
    {
        public static Gamepad current { get; private set; }
        public ButtonControl buttonWest { get; protected set; }
        public ButtonControl buttonNorth { get; protected set; }
        public ButtonControl buttonSouth { get; protected set; }
        public ButtonControl buttonEast { get; protected set; }
        public ButtonControl leftStickButton { get; protected set; }
        public ButtonControl rightStickButton { get; protected set; }
        public ButtonControl startButton { get; protected set; }
        public ButtonControl selectButton { get; protected set; }
        public DpadControl dpad { get; protected set; }
        public ButtonControl leftShoulder { get; protected set; }
        public ButtonControl rightShoulder { get; protected set; }
        public StickControl leftStick { get; protected set; }
        public StickControl rightStick { get; protected set; }
        public ButtonControl leftTrigger { get; protected set; }
        public ButtonControl rightTrigger { get; protected set; }
        public virtual void PauseHaptics() { }
        public virtual void ResetHaptics() { }
        public virtual void SetMotorSpeeds(float lowFrequency, float highFrequency) { }
    }
}
