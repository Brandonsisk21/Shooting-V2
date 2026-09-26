using System.Collections.Generic;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>Placeholder sounds synthesized at runtime so Phase 1 needs no audio assets.</summary>
    public static class ProceduralAudio
    {
        private const int SampleRate = 44100;
        private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        public static AudioClip Gunshot(string weaponId) => weaponId == "sniper"
            ? Get("shot_sniper", () => Noise("shot_sniper", 0.45f, decay: 9f, lowpass: 0.25f, gain: 0.9f))
            : Get("shot_rifle", () => Noise("shot_rifle", 0.14f, decay: 28f, lowpass: 0.5f, gain: 0.6f));

        public static AudioClip HitBody => Get("hit_body", () => Tone("hit_body", 0.05f, 1700f, 1700f, 0.35f));
        public static AudioClip HitHead => Get("hit_head", () => Tone("hit_head", 0.11f, 2600f, 2600f, 0.4f));
        public static AudioClip Kill => Get("kill", () => Tone("kill", 0.22f, 900f, 450f, 0.5f));
        public static AudioClip Pickup => Get("pickup", () => Tone("pickup", 0.12f, 600f, 1200f, 0.35f));
        public static AudioClip Reload => Get("reload", () => Noise("reload", 0.06f, decay: 40f, lowpass: 0.9f, gain: 0.3f));
        public static AudioClip PowerWeaponSpawn => Get("power_spawn", () => Tone("power_spawn", 0.6f, 300f, 900f, 0.5f));

        private static AudioClip Get(string key, System.Func<AudioClip> build)
        {
            if (!Cache.TryGetValue(key, out var clip) || clip == null)
            {
                clip = build();
                Cache[key] = clip;
            }
            return clip;
        }

        private static AudioClip Noise(string name, float seconds, float decay, float lowpass, float gain)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            var rng = new System.Random(name.GetHashCode());
            float filtered = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                filtered += (white - filtered) * lowpass;
                data[i] = filtered * Mathf.Exp(-decay * t) * gain;
            }
            return Create(name, data);
        }

        private static AudioClip Tone(string name, float seconds, float startHz, float endHz, float gain)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float progress = (float)i / count;
                float hz = Mathf.Lerp(startHz, endHz, progress);
                phase += 2f * Mathf.PI * hz / SampleRate;
                float envelope = Mathf.Min(1f, progress * 40f) * (1f - progress);
                data[i] = Mathf.Sin(phase) * envelope * gain;
            }
            return Create(name, data);
        }

        private static AudioClip Create(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
