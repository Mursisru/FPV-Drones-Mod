using FPVMod.Access;
using UnityEngine;

namespace FPVMod.FpvView
{
    internal readonly struct FpvInfraredExposureBreakdown
    {
        internal readonly float PolicyExposure;
        internal readonly float VanillaExposure;
        internal readonly bool SyncedVanilla;
        internal readonly float FinalExposure;

        internal FpvInfraredExposureBreakdown(
            float policyExposure,
            float vanillaExposure,
            bool syncedVanilla,
            float finalExposure)
        {
            PolicyExposure = policyExposure;
            VanillaExposure = vanillaExposure;
            SyncedVanilla = syncedVanilla;
            FinalExposure = finalExposure;
        }
    }

    /// <summary>TargetCam IR exposure sync (MC InfraredExposure port).</summary>
    internal static class FpvInfraredExposure
    {
        internal static float Resolve(float policyExposure, out FpvInfraredExposureBreakdown breakdown)
        {
            float vanillaExposure = policyExposure;
            bool syncedVanilla = FpvTargetCamAccess.TryGetVanillaIrSnapshot(
                out bool vanillaIr,
                out float liveVanillaExposure,
                out _)
                && vanillaIr;

            if (syncedVanilla)
                vanillaExposure = liveVanillaExposure;

            float baseExposure = syncedVanilla ? vanillaExposure : policyExposure;
            float finalExposure = Mathf.Clamp(baseExposure, -4f, 4f);

            breakdown = new FpvInfraredExposureBreakdown(
                policyExposure,
                vanillaExposure,
                syncedVanilla,
                finalExposure);
            return finalExposure;
        }
    }
}
