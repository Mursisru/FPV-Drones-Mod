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
                    if (GameplayUI.GameIsPaused || Time.timeScale < 0.01f)
                        return true;

                    // During FPV: only real pause. MC reset often leaves menuCanvas on /
                    // flightControlsEnabled=false — that must NOT freeze the next drone.
                    if (Session.FpvControlSession.Active)
                        return false;

                    var ui = SceneSingleton<GameplayUI>.i;
                    if (ui != null && ui.menuCanvas != null && ui.menuCanvas.enabled)
                        return true;
                    if (!GameManager.flightControlsEnabled)
                        return true;
                }
                catch
                {
                    // ignore
                }

                return false;
            }
        }

        internal static bool MenuOpen => BlocksFlightInput;
    }
}
