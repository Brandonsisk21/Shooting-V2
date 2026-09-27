using System;
using ArenaShooter.Core;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Player preferences from the Settings menu, saved with PlayerPrefs so they survive restarts,
    /// and applied live to the local player whenever they change.
    /// </summary>
    public static class GameSettings
    {
        // Baselines the multipliers scale (match PlayerInputReader's defaults).
        public const float BaseMouseDegreesPerPixel = 0.12f;
        public const float BaseStickYaw = 200f;
        public const float BaseStickPitch = 130f;
        public const float AimAssistFriction = 0.55f;

        public static float MouseSensitivity = 1f;
        public static float StickSensitivity = 1f;
        public static bool InvertY;
        public static bool AimAssist = true;
        public static bool Rumble = true;
        public static float FieldOfView = 60f;
        public static float Volume = 0.8f;
        public static bool ShowControlsHint = true;
        public static MatchSetup LastSetup = new MatchSetup();

        public static event Action Changed;

        private const string Prefix = "arena.";

        public static void Load()
        {
            MouseSensitivity = PlayerPrefs.GetFloat(Prefix + "mouseSens", 1f);
            StickSensitivity = PlayerPrefs.GetFloat(Prefix + "stickSens", 1f);
            InvertY = PlayerPrefs.GetInt(Prefix + "invertY", 0) == 1;
            AimAssist = PlayerPrefs.GetInt(Prefix + "aimAssist", 1) == 1;
            Rumble = PlayerPrefs.GetInt(Prefix + "rumble", 1) == 1;
            FieldOfView = PlayerPrefs.GetFloat(Prefix + "fov", 60f);
            Volume = PlayerPrefs.GetFloat(Prefix + "volume", 0.8f);
            ShowControlsHint = PlayerPrefs.GetInt(Prefix + "hint", 1) == 1;

            var d = new MatchSetup();
            LastSetup = new MatchSetup
            {
                map = (MapChoice)PlayerPrefs.GetInt(Prefix + "map", (int)d.map),
                botCount = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "bots", d.botCount), 0, MatchSetup.MaxBots),
                difficulty = (Difficulty)PlayerPrefs.GetInt(Prefix + "difficulty", (int)d.difficulty),
                scoreLimit = PlayerPrefs.GetInt(Prefix + "scoreLimit", d.scoreLimit),
                timeLimitMinutes = PlayerPrefs.GetInt(Prefix + "timeLimit", d.timeLimitMinutes),
            };
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "mouseSens", MouseSensitivity);
            PlayerPrefs.SetFloat(Prefix + "stickSens", StickSensitivity);
            PlayerPrefs.SetInt(Prefix + "invertY", InvertY ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "aimAssist", AimAssist ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "rumble", Rumble ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "fov", FieldOfView);
            PlayerPrefs.SetFloat(Prefix + "volume", Volume);
            PlayerPrefs.SetInt(Prefix + "hint", ShowControlsHint ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "map", (int)LastSetup.map);
            PlayerPrefs.SetInt(Prefix + "bots", LastSetup.botCount);
            PlayerPrefs.SetInt(Prefix + "difficulty", (int)LastSetup.difficulty);
            PlayerPrefs.SetInt(Prefix + "scoreLimit", LastSetup.scoreLimit);
            PlayerPrefs.SetInt(Prefix + "timeLimit", LastSetup.timeLimitMinutes);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void ApplyTo(PlayerInputReader input, PlayerLook look)
        {
            if (input != null)
            {
                input.mouseDegreesPerPixel = BaseMouseDegreesPerPixel * MouseSensitivity;
                input.stickYawSpeed = BaseStickYaw * StickSensitivity;
                input.stickPitchSpeed = BaseStickPitch * StickSensitivity;
                input.invertLookY = InvertY;
                input.aimAssistFriction = AimAssist ? AimAssistFriction : 1f;
                input.rumble = Rumble;
            }
            if (look != null) look.baseFov = FieldOfView;
        }
    }
}
