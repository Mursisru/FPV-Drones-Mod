using System;
using System.Collections;
using System.Reflection;
using FPVMod.Audio;
using HarmonyLib;
using UnityEngine;

namespace FPVMod.HarmonyPatches
{
    /// <summary>
    /// Explosion distance uses CSM by default — during FPV route to feed cam.
    /// </summary>
    [HarmonyPatch(typeof(ExplosionAudioManager), "Update")]
    internal static class FpvExplosionAudioManagerPatch
    {
        private static readonly FieldInfo? SourcesField =
            AccessTools.Field(typeof(ExplosionAudioManager), "sources");

        private static bool Prefix(ExplosionAudioManager __instance)
        {
            if (!FpvListenerBridge.Active || SourcesField == null)
                return true;

            if (SourcesField.GetValue(__instance) is not IList sources)
                return true;

            Vector3 listener = FpvListenerBridge.WorldPosition;
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                object? item = sources[i];
                if (item == null)
                {
                    sources.RemoveAt(i);
                    continue;
                }

                Type t = item.GetType();
                MethodInfo? audioNull = AccessTools.Method(t, "AudioSourceNull");
                MethodInfo? inRange = AccessTools.Method(t, "InRange");
                if (audioNull == null || inRange == null)
                    continue;

                if ((bool)audioNull.Invoke(item, null)!)
                {
                    sources.RemoveAt(i);
                    continue;
                }

                if ((bool)inRange.Invoke(item, new object[] { listener })!)
                    sources.RemoveAt(i);
            }

            if (sources.Count == 0)
                __instance.enabled = false;

            return false;
        }
    }

    /// <summary>Queue explosions near drone, not near parked jet / spectator CSM.</summary>
    [HarmonyPatch(typeof(ExplosionAudio), "Start")]
    internal static class FpvExplosionAudioStartPatch
    {
        private static readonly FieldInfo? SoundsField =
            AccessTools.Field(typeof(ExplosionAudio), "explosionSounds");

        private static bool Prefix(ExplosionAudio __instance)
        {
            if (!FpvListenerBridge.Active || SoundsField == null)
                return true;

            object? raw = SoundsField.GetValue(__instance);
            if (raw == null)
            {
                UnityEngine.Object.Destroy(__instance);
                return false;
            }

            var filter = __instance.gameObject.AddComponent<AudioLowPassFilter>();
            Vector3 listener = FpvListenerBridge.WorldPosition;
            float distSq = (listener - __instance.transform.position).sqrMagnitude;

            if (raw is Array arr)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    object? es = arr.GetValue(i);
                    if (es == null)
                        continue;

                    Type t = es.GetType();
                    var source = AccessTools.Field(t, "source")?.GetValue(es) as AudioSource;
                    var clips = AccessTools.Field(t, "clips")?.GetValue(es) as AudioClip[];
                    float yield = 0f;
                    object? yObj = AccessTools.Field(t, "yield")?.GetValue(es);
                    if (yObj is float yf)
                        yield = yf;

                    if (source == null || clips == null || clips.Length == 0)
                        continue;

                    float maxD = source.maxDistance;
                    if (distSq >= maxD * maxD)
                        continue;

                    source.clip = clips[UnityEngine.Random.Range(0, clips.Length)];
                    source.pitch += UnityEngine.Random.Range(-0.2f, 0.2f);
                    source.dopplerLevel = 0f;
                    source.spatialBlend = 1f;
                    try
                    {
                        SceneSingleton<ExplosionAudioManager>.i.AddExplosionAudio(source, filter, yield);
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }

            UnityEngine.Object.Destroy(__instance);
            return false;
        }
    }

    /// <summary>ManagedExplosion.Play — shake/attenuation from FPV listener, not CSM.</summary>
    [HarmonyPatch]
    internal static class FpvManagedExplosionPlayPatch
    {
        private static readonly Type? NestedType =
            AccessTools.Inner(typeof(ExplosionAudioManager), "ManagedExplosion");

        private static MethodBase? TargetMethod() =>
            NestedType == null ? null : AccessTools.Method(NestedType, "Play");

        private static readonly FieldInfo? AudioField =
            NestedType == null ? null : AccessTools.Field(NestedType, "audioSource");
        private static readonly FieldInfo? FilterField =
            NestedType == null ? null : AccessTools.Field(NestedType, "filter");
        private static readonly FieldInfo? StartField =
            NestedType == null ? null : AccessTools.Field(NestedType, "startTime");
        private static readonly FieldInfo? YieldField =
            NestedType == null ? null : AccessTools.Field(NestedType, "yield");
        private static readonly FieldInfo? XformField =
            NestedType == null ? null : AccessTools.Field(NestedType, "xform");

        private static bool Prefix(object __instance)
        {
            if (!FpvListenerBridge.Active)
                return true;
            if (AudioField == null || FilterField == null || StartField == null
                || YieldField == null || XformField == null)
                return true;

            try
            {
                var audio = AudioField.GetValue(__instance) as AudioSource;
                var filter = FilterField.GetValue(__instance) as AudioLowPassFilter;
                if (audio == null || filter == null)
                    return true;

                float startTime = (float)StartField.GetValue(__instance)!;
                float yield = (float)YieldField.GetValue(__instance)!;
                var xform = XformField.GetValue(__instance) as Transform;

                float num = Time.timeSinceLevelLoad - startTime;
                audio.bypassListenerEffects = num > 0.1f;
                filter.cutoffFrequency = Mathf.Clamp(22000f / Mathf.Max(num, 0.001f), 1000f, 22000f);
                audio.Play();

                if (yield > 0f && xform != null)
                {
                    float num2 = Vector3.SqrMagnitude(xform.position - FpvListenerBridge.WorldPosition);
                    if (num2 < 1f)
                        num2 = 1f;
                    float value = yield * 100f / num2;
                    SceneSingleton<CameraStateManager>.i?.ShakeCamera(Mathf.Clamp01(value), 0f);
                }
            }
            catch
            {
                return true;
            }

            return false;
        }
    }
}
