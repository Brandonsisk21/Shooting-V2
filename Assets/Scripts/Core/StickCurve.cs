using System;

namespace ArenaShooter.Core
{
    /// <summary>
    /// Controller thumbstick shaping: a radial deadzone (drift-free center) rescaled so output still
    /// starts at 0, then a power curve so small deflections give fine aim and full tilt turns fast.
    /// </summary>
    public static class StickCurve
    {
        public static void Apply(float x, float y, float deadzone, float exponent, out float outX, out float outY)
        {
            float magnitude = (float)Math.Sqrt(x * x + y * y);
            if (magnitude <= deadzone || magnitude <= 0f)
            {
                outX = 0f;
                outY = 0f;
                return;
            }

            float clamped = Math.Min(1f, magnitude);
            float rescaled = (clamped - deadzone) / (1f - deadzone);
            float shaped = (float)Math.Pow(rescaled, exponent);
            outX = x / magnitude * shaped;
            outY = y / magnitude * shaped;
        }
    }
}
