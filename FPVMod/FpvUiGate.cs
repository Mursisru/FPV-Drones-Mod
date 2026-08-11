using UnityEngine;

namespace FPVMod
{
    /// <summary>Pause / menu — don't eat sticks or block UI with FPV overlays.</summary>
    internal static class FpvUiGate
    {
        internal static bool BlocksFlightInput
        {
            get
            {
                try
                {
                    if (GameplayUI.GameIsPaused)
                        return true;
                    if (!GameManager.flightControlsEnabled)
                        return true;
                    var ui = SceneSingleton<GameplayUI>.i;
                    if (ui != null && ui.menuCanvas != null && ui.menuCanvas.enabled)
                        return true;
                }
                catch
                {
                    // ignore
                }

                return Time.timeScale < 0.01f;
            }
        }

        internal static bool MenuOpen => BlocksFlightInput;
    }
}
