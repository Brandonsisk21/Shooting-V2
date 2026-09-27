using System.Collections.Generic;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Goofy synthesized sound effects (GDD 5.1: "pew" layered over punchy impacts), generated at
    /// runtime so no audio files are needed yet.
    /// </summary>
    public static class ProceduralAudio
    {
        private const int SampleRate = 44100;
        private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        /// <summary>Pew Rifle: bright falling "pew". Long Zapper: big descending "PYOOOM" with a crackle.</summary>
        public static AudioClip Gunshot(string weaponId) => weaponId == "sniper"
            ? Get("zap", () => Sweep("zap", 0.5f, 1300f, 70f, square: 0.35f, noise: 0.35f, gain: 0.75f))
            : Get("pew", () => Sweep("pew", 0.14f, 1900f, 380f, square: 0.5f, noise: 0.12f, gain: 0.5f));

        public static AudioClip HitBody => Get("hit_body", () => Sweep("hit_body", 0.06f, 950f, 700f, square: 0.2f, noise: 0f, gain: 0.35f));
        public static AudioClip HitHead => Get("hit_head", () => Ding("hit_head", 0.18f, 2400f, 0.4f));
        public static AudioClip Kill => Get("kill", () => Sweep("kill", 0.32f, 650f, 120f, square: 0.6f, noise: 0.25f, gain: 0.5f));
        public static AudioClip Pickup => Get("pickup", () => Sweep("pickup", 0.14f, 500f, 1400f, square: 0.3f, noise: 0f, gain: 0.35f));
        public static AudioClip Reload => Get("reload", () => Sweep("reload", 0.1f, 300f, 900f, square: 0.8f, noise: 0.2f, gain: 0.25f));
        public static AudioClip PowerWeaponSpawn => Get("power_spawn", () => Wobble("power_spawn", 0.8f, 250f, 800f, 0.45f));
        /// <summary>Confetti "pop" when a grunt goes down.</summary>
        public static AudioClip Pop => Get("pop", () => Sweep("pop", 0.08f, 1500f, 200f, square: 0f, noise: 0.6f, gain: 0.5f));

        private static AudioClip Get(string key, System.Func<AudioClip> build)
        {
            if (!Cache.TryGetValue(key, out var clip) || clip == null)
            {
                clip = build();
                Cache[key] = clip;
            }
            return clip;
        }

        /// <summary>Exponential pitch sweep mixing sine and square (chunky toy tone) with some noise.</summary>
        private static AudioClip Sweep(string name, float seconds, float startHz, float endHz, float square, float noise, float gain)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            var rng = new System.Random(name.GetHashCode());
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float progress = (float)i / count;
                float hz = startHz * Mathf.Pow(endHz / startHz, progress);
                phase += 2f * Mathf.PI * hz / SampleRate;
                float sine = Mathf.Sin(phase);
                float tone = Mathf.Lerp(sine, Mathf.Sign(sine), square);
                float n = (float)(rng.NextDouble() * 2.0 - 1.0) * noise * (1f - progress);
                float envelope = Mathf.Min(1f, progress * 60f) * Mathf.Pow(1f - progress, 1.5f);
                data[i] = (tone * (1f - noise * 0.5f) + n) * envelope * gain;
            }
            return Create(name, data);
        }

        /// <summary>Bell-like "ding" (fundamental + bright overtone) for headshots.</summary>
        private static AudioClip Ding(string name, float seconds, float hz, float gain)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float progress = (float)i / count;
                float tone = Mathf.Sin(2f * Mathf.PI * hz * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * hz * 2.76f * t);
                data[i] = tone * Mathf.Min(1f, progress * 80f) * Mathf.Exp(-progress * 5f) * gain * 0.66f;
            }
            return Create(name, data);
        }

        /// <summary>Rising, wobbling "wub-wub" for the sniper spawning.</summary>
        private static AudioClip Wobble(string name, float seconds, float startHz, float endHz, float gain)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float progress = (float)i / count;
                float t = (float)i / SampleRate;
                float hz = Mathf.Lerp(startHz, endHz, progress) * (1f + 0.08f * Mathf.Sin(2f * Mathf.PI * 9f * t));
                phase += 2f * Mathf.PI * hz / SampleRate;
                float envelope = Mathf.Min(1f, progress * 30f) * (1f - progress);
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
