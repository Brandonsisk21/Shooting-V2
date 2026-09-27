namespace ArenaShooter.Core
{
    public enum CrouchMode
    {
        /// <summary>Crouched while the button is held.</summary>
        Hold,
        /// <summary>Each press switches between crouched and standing.</summary>
        Toggle,
    }

    /// <summary>
    /// Turns the crouch button into "wants to crouch" for the chosen mode (Settings → Crouch).
    /// In toggle mode, jumping or dying stands you back up.
    /// </summary>
    public sealed class CrouchInput
    {
        public bool Toggled { get; private set; }

        public bool Update(CrouchMode mode, bool held, bool pressed, bool jump)
        {
            if (mode == CrouchMode.Hold)
            {
                Toggled = false;
                return held;
            }
            if (pressed) Toggled = !Toggled;
            if (jump) Toggled = false;
            return Toggled;
        }

        public void Reset() => Toggled = false;
    }
}
