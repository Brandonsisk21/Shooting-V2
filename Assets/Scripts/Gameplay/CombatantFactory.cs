using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Builds a third-person grunt: CharacterController body, motor, health, weapons and the Space
    /// Grunt model. Used for bots, and online for other players (network-driven).
    /// </summary>
    public static class CombatantFactory
    {
        /// <param name="networkDriven">
        /// True for grunts moved by the network (other players online): their weapon only shows
        /// the held gun and shot effects, and a <see cref="NetProxy"/> drives their position.
        /// </param>
        public static Combatant CreateBody(string name, Color color, Transform parent, bool networkDriven)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.4f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.05f;

            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0f, 1.6f, 0f);

            root.AddComponent<PlayerMotor>();
            root.AddComponent<Health>();

            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.minDistance = 4f;
            audio.maxDistance = 90f;

            var combatant = root.AddComponent<Combatant>();
            combatant.displayName = name;
            combatant.color = color;
            combatant.Eyes = head;

            var weapons = root.AddComponent<WeaponHolder>();
            weapons.aim = head;
            weapons.audioSource = audio;
            weapons.isProxy = networkDriven;
            weapons.thirdPersonGunMount = CombatantBody.Build(combatant, head, color, visible: true);
            root.AddComponent<GrenadeThrower>();

            if (networkDriven)
            {
                var proxy = root.AddComponent<NetProxy>();
                proxy.eyes = head;
            }
            return combatant;
        }
    }
}
