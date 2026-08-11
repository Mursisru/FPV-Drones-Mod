using FPVMod.FpvView;
using UnityEngine;

namespace FPVMod.Audio
{
    /// <summary>
    /// FPV: world audio from drone feed, not CSM/cockpit/spectator.
    /// Disables CameraStateManager AudioListener; enables one on feed rig.
    /// </summary>
    internal static class FpvListenerBridge
    {
        private static AudioListener? _csmListener;
        private static AudioListener? _feedListener;
        private static bool _active;
        private static bool _heldDopplerForced;
        private static Aircraft? _heldAircraft;

        internal static bool Active => _active;

        /// <summary>World pos for explosion/sonic distance checks during FPV.</summary>
        internal static Vector3 WorldPosition
        {
            get
            {
                if (_active)
                {
                    Camera? feed = FpvFeedCamera.FeedCamera;
                    if (feed != null)
                        return feed.transform.position;
                }

                try
                {
                    var csm = SceneSingleton<CameraStateManager>.i;
                    if (csm != null)
                        return csm.transform.position;
                }
                catch
                {
                    // ignore
                }

                return Vector3.zero;
            }
        }

        internal static void Enter(Aircraft? heldAircraft)
        {
            EnsureCsmListener();
            EnsureFeedListener();
            if (_feedListener == null)
                return;

            if (_csmListener != null)
                _csmListener.enabled = false;
            _feedListener.enabled = true;
            _active = true;

            _heldAircraft = heldAircraft;
            ForceHeldExterior(heldAircraft);
        }

        internal static void Exit()
        {
            if (_feedListener != null)
                _feedListener.enabled = false;

            if (_csmListener != null)
                _csmListener.enabled = true;
            else
                EnsureCsmListenerEnabled();

            RestoreHeldDoppler();
            _heldAircraft = null;
            _active = false;
        }

        private static void ForceHeldExterior(Aircraft? ac)
        {
            _heldDopplerForced = false;
            if (ac == null)
                return;

            // Cockpit uses spatialBlend=0 — jet would scream over FPV. Force 3D for session.
            try
            {
                ac.SetDoppler(true);
                _heldDopplerForced = true;
            }
            catch
            {
                _heldDopplerForced = false;
            }
        }

        private static void RestoreHeldDoppler()
        {
            if (!_heldDopplerForced || _heldAircraft == null)
                return;

            try
            {
                var csm = SceneSingleton<CameraStateManager>.i;
                bool cockpit = csm != null && csm.currentState == csm.cockpitState
                    && csm.followingUnit == _heldAircraft;
                _heldAircraft.SetDoppler(!cockpit);
            }
            catch
            {
                // ignore
            }

            _heldDopplerForced = false;
        }

        private static void EnsureCsmListener()
        {
            if (_csmListener != null)
                return;

            try
            {
                var csm = SceneSingleton<CameraStateManager>.i;
                if (csm == null)
                    return;
                _csmListener = csm.GetComponent<AudioListener>();
                if (_csmListener == null)
                    _csmListener = csm.gameObject.AddComponent<AudioListener>();
            }
            catch
            {
                // ignore
            }
        }

        private static void EnsureCsmListenerEnabled()
        {
            EnsureCsmListener();
            if (_csmListener != null)
                _csmListener.enabled = true;
        }

        private static void EnsureFeedListener()
        {
            Camera? feed = FpvFeedCamera.FeedCamera;
            if (feed == null)
                return;

            _feedListener = feed.GetComponent<AudioListener>();
            if (_feedListener == null)
                _feedListener = feed.gameObject.AddComponent<AudioListener>();
        }
    }
}
