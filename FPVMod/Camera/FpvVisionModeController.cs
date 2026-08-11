using UnityEngine;

namespace FPVMod.FpvView
{
    /// <summary>FS vision cycle (J) — Color / NVG / WH / BH / EDGE± (MC parity).</summary>
    internal static class FpvVisionModeController
    {
        private static readonly FpvVisionMode[] Order =
        {
            FpvVisionMode.Color,
            FpvVisionMode.NightVision,
            FpvVisionMode.WhiteHot,
            FpvVisionMode.BlackHot,
            FpvVisionMode.WhiteContour,
            FpvVisionMode.BlackContour
        };

        private static FpvVisionMode _mode = FpvVisionMode.WhiteHot;

        internal static FpvVisionMode Mode => _mode;

        internal static void Reset() => _mode = FpvVisionMode.WhiteHot;

        internal static void Cycle()
        {
            int index = 0;
            for (int i = 0; i < Order.Length; i++)
            {
                if (Order[i] == _mode)
                {
                    index = i;
                    break;
                }
            }
            _mode = Order[(index + 1) % Order.Length];
            FpvPlugin.ModLogger?.LogInfo("FPV vision → " + _mode);
        }

        internal static bool UsesInfraredBlit(FpvVisionMode mode) =>
            mode == FpvVisionMode.WhiteHot
            || mode == FpvVisionMode.BlackHot
            || mode == FpvVisionMode.WhiteContour
            || mode == FpvVisionMode.BlackContour;

        internal static bool UsesNightVisionVolume(FpvVisionMode mode) =>
            mode == FpvVisionMode.NightVision;

        internal static string GunshipLabel(FpvVisionMode mode) =>
            mode switch
            {
                FpvVisionMode.NightVision => "NVG",
                FpvVisionMode.WhiteHot => "WH",
                FpvVisionMode.BlackHot => "BH",
                FpvVisionMode.WhiteContour => "EDGE+",
                FpvVisionMode.BlackContour => "EDGE-",
                _ => "TV"
            };

        internal static void TickInput(bool sessionActive)
        {
            if (!sessionActive || FpvUiGate.MenuOpen || FpvUiGate.BlocksFlightInput)
                return;
            if (UnityEngine.Input.GetKeyDown(KeyCode.J))
                Cycle();
        }
    }
}
