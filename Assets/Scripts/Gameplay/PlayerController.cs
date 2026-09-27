using UnityEngine;
using UnityEngine.InputSystem;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Turns <see cref="PlayerCommands"/> (keyboard/mouse or gamepad) into movement, look and
    /// weapon actions. Death and respawn are handled by <see cref="MatchManager"/>.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public PlayerMotor motor;
        public PlayerLook look;
        public WeaponHolder weapons;
        public Health health;
        public PlayerInputReader input;
        [Tooltip("Debug: press K to hurt yourself for this much, to test regen.")]
        public float debugSelfDamage = 25f;

        public bool IsDead => health != null && health.IsDead;
        public PlayerCommands LastCommands { get; private set; }

        private void Start()
        {
            weapons.Fired += stats =>
            {
                if (stats.id == "sniper") input.Rumble(0.45f, 0.7f, 0.14f);
                else input.Rumble(0.05f, 0.25f, 0.05f);
            };
            health.Damaged += (_, result, __) => input.Rumble(0.5f, 0.3f, result.Killed ? 0.35f : 0.18f);
            GetComponent<Combatant>().Respawned += _ => look.ResetPitch();
        }

        private void Update()
        {
            if (ArenaBootstrap.IsPaused)
            {
                LastCommands = default;
                return;
            }

            // After alt-tabbing the mouse can be released; a click captures it again.
            var mouse = Mouse.current;
            if (!CursorLocked && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                SetCursorLocked(true);
                return; // the click that captures the mouse shouldn't also fire
            }

            var cmd = input.Read(Time.deltaTime, weapons.NearbyPickup != null, weapons.AimTarget != null, CursorLocked);
            LastCommands = cmd;

            if (IsDead)
            {
                motor.Move(Vector2.zero, false, Time.deltaTime);
                return;
            }

            motor.Move(cmd.Move, cmd.Jump, Time.deltaTime);
            look.Zoom = weapons.CurrentZoom;
            look.Look(cmd.LookDegrees);

            if (cmd.Fire) weapons.QueueFire();
            if (cmd.ToggleZoom) weapons.ToggleZoom();
            if (cmd.Reload) weapons.Reload();
            if (cmd.Pickup) weapons.TryPickup();
            if (cmd.SwitchWeapon) weapons.SwitchNext();
            if (cmd.SwitchToSlot >= 0) weapons.SwitchTo(cmd.SwitchToSlot);
            if (cmd.DebugSelfDamage && !weapons.remoteAuthority) health.TakeDamage(debugSelfDamage); // offline/host only
        }

        private static bool CursorLocked => Cursor.lockState == CursorLockMode.Locked;

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
