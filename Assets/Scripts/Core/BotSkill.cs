using System;

namespace ArenaShooter.Core
{
    /// <summary>
    /// How well a bot plays. Bots see, react, turn and aim with human-like limits instead of
    /// snapping to targets. Tunable in the inspector; presets below.
    /// </summary>
    [Serializable]
    public class BotSkill
    {
        /// <summary>Seconds between first seeing an enemy and starting to shoot.</summary>
        public float reactionTime = 0.4f;
        /// <summary>Aim error (degrees) right after acquiring a target.</summary>
        public float initialAimError = 6f;
        /// <summary>Aim error (degrees) after tracking a target for a while.</summary>
        public float settledAimError = 1.2f;
        /// <summary>Seconds of continuous tracking to go from initial to settled aim error.</summary>
        public float aimSettleTime = 1.2f;
        /// <summary>Max turn speed in degrees per second.</summary>
        public float turnSpeed = 300f;
        /// <summary>Chance (0-1) to aim at the head rather than the body when acquiring a target.</summary>
        public float headshotChance = 0.3f;
        /// <summary>Only fires when the crosshair is within this many degrees of the aim point.</summary>
        public float fireTolerance = 2.5f;
        public float viewDistance = 70f;
        /// <summary>Full width of the bot's vision cone in degrees.</summary>
        public float fieldOfView = 140f;

        public static BotSkill Easy() => new BotSkill
        {
            reactionTime = 0.65f, initialAimError = 9f, settledAimError = 2.5f, aimSettleTime = 1.6f,
            turnSpeed = 200f, headshotChance = 0.1f, fireTolerance = 3f, viewDistance = 55f, fieldOfView = 120f,
        };

        public static BotSkill Normal() => new BotSkill();

        public static BotSkill Hard() => new BotSkill
        {
            reactionTime = 0.25f, initialAimError = 4f, settledAimError = 0.6f, aimSettleTime = 0.8f,
            turnSpeed = 420f, headshotChance = 0.55f, fireTolerance = 2f, viewDistance = 90f, fieldOfView = 160f,
        };

        /// <summary>Aim error cone (degrees) after tracking the current target for <paramref name="trackingTime"/> seconds.</summary>
        public float AimErrorAfter(float trackingTime)
        {
            if (aimSettleTime <= 0f) return settledAimError;
            float t = Math.Max(0f, Math.Min(1f, trackingTime / aimSettleTime));
            return initialAimError + (settledAimError - initialAimError) * t;
        }

        /// <summary>Whether a point at the given bearing and distance falls inside the bot's vision cone.</summary>
        public bool CanSee(float angleFromForwardDegrees, float distance) =>
            distance <= viewDistance && Math.Abs(angleFromForwardDegrees) <= fieldOfView * 0.5f;
    }
}
