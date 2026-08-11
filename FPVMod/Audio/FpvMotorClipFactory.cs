using UnityEngine;

namespace FPVMod.Audio
{
    /// <summary>Procedural FPV motor + air loops (no bundle clips required).</summary>
    internal static class FpvMotorClipFactory
    {
        private const int SampleRate = 22050;
        private const float MotorLenSec = 0.55f;
        private const float AirLenSec = 0.8f;

        private static AudioClip? _motor;
        private static AudioClip? _air;

        internal static AudioClip MotorLoop => _motor ??= BuildMotor();
        internal static AudioClip AirLoop => _air ??= BuildAir();

        private static AudioClip BuildMotor()
        {
            int n = Mathf.Max(256, (int)(SampleRate * MotorLenSec));
            var data = new float[n];
            // Four slightly detuned blade tones + grit → multirotor buzz.
            float[] freqs = { 92f, 97f, 188f, 275f };
            float[] amps = { 0.38f, 0.32f, 0.22f, 0.14f };
            uint seed = 0xA5F31u;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float s = 0f;
                for (int h = 0; h < freqs.Length; h++)
                {
                    float ph = t * freqs[h] * Mathf.PI * 2f;
                    // Soft square-ish (odd harmonics) for prop edge.
                    s += amps[h] * (Mathf.Sin(ph) + 0.35f * Mathf.Sin(ph * 3f) + 0.15f * Mathf.Sin(ph * 5f));
                }

                seed = seed * 1664525u + 1013904223u;
                float noise = (seed / (float)uint.MaxValue) * 2f - 1f;
                s += noise * 0.12f;
                data[i] = Mathf.Clamp(s * 0.42f, -1f, 1f);
            }

            FadeEdges(data, SampleRate / 80);
            var clip = AudioClip.Create("FPVMod.MotorLoop", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildAir()
        {
            int n = Mathf.Max(256, (int)(SampleRate * AirLenSec));
            var data = new float[n];
            uint seed = 0xC0FFEEu;
            float lp = 0f;

            for (int i = 0; i < n; i++)
            {
                seed = seed * 1664525u + 1013904223u;
                float white = (seed / (float)uint.MaxValue) * 2f - 1f;
                // One-pole low-pass → wind whoosh.
                lp = Mathf.Lerp(lp, white, 0.08f);
                data[i] = Mathf.Clamp(lp * 0.55f, -1f, 1f);
            }

            FadeEdges(data, SampleRate / 60);
            var clip = AudioClip.Create("FPVMod.AirLoop", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void FadeEdges(float[] data, int fade)
        {
            fade = Mathf.Clamp(fade, 1, data.Length / 4);
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                data[i] *= w;
                data[data.Length - 1 - i] *= w;
            }
        }
    }
}
