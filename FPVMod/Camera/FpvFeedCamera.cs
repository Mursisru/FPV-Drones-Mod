using UnityEngine;
using UnityEngine.UI;

namespace FPVMod.FpvView
{
    /// <summary>
    /// Dedicated FPV feed — locked to airframe (full acro, no horizon lock).
    /// Never parents CameraStateManager.
    /// </summary>
    internal static class FpvFeedCamera
    {
        private const float NoseLocalZ = 1.2f;
        private const int TexW = 1280;
        private const int TexH = 720;

        private static GameObject? _rigGo;
        private static Camera? _cam;
        private static RenderTexture? _rt;
        private static GameObject? _overlayGo;
        private static RawImage? _raw;
        private static Missile? _missile;
        private static bool _active;

        internal static bool IsActive => _active && _missile != null;

        internal static void Attach(Missile drone)
        {
            if (drone == null)
                return;

            Detach();
            EnsureRig();
            EnsureOverlay();
            if (_rigGo == null || _cam == null || _raw == null)
                return;

            _missile = drone;
            _rigGo.transform.SetParent(drone.transform, false);
            _rigGo.transform.localPosition = new Vector3(0f, 0.05f, NoseLocalZ);
            _rigGo.transform.localRotation = Quaternion.identity;
            _cam.enabled = true;
            _cam.fieldOfView = FpvConstants.CameraFov;

            if (_overlayGo != null)
                _overlayGo.SetActive(true);
            _raw.texture = _rt;
            _active = true;

            try { FlightHud.EnableCanvas(false); } catch { /* ignore */ }
            FpvPlugin.ModLogger?.LogInfo("FPV: body-locked feed camera (no horizon limit).");
        }

        internal static void LateTick()
        {
            if (!_active || _missile == null || _missile.disabled || _rigGo == null)
                return;

            _rigGo.transform.localPosition = new Vector3(0f, 0.05f, NoseLocalZ);
            _rigGo.transform.localRotation = Quaternion.identity;
        }

        /// <summary>Dim/hide feed overlay while pause menu is up (clicks pass through anyway).</summary>
        internal static void TickPauseUi()
        {
            if (_overlayGo == null || !_active)
                return;
            var cg = _overlayGo.GetComponent<CanvasGroup>();
            if (cg != null)
                cg.alpha = FpvUiGate.MenuOpen ? 0.15f : 1f;
        }

        internal static void Detach()
        {
            _active = false;
            _missile = null;
            if (_cam != null)
                _cam.enabled = false;
            if (_rigGo != null && _rigGo.transform.parent != null)
                _rigGo.transform.SetParent(null, true);
            if (_overlayGo != null)
                _overlayGo.SetActive(false);
            try
            {
                var csm = SceneSingleton<CameraStateManager>.i;
                FlightHud.EnableCanvas(csm != null && csm.currentState == csm.cockpitState);
            }
            catch
            {
                // ignore
            }
        }

        private static void EnsureRig()
        {
            if (_rigGo != null && _cam != null && _rt != null)
                return;

            _rigGo = new GameObject("FPVMod.FeedCam");
            Object.DontDestroyOnLoad(_rigGo);
            _cam = _rigGo.AddComponent<Camera>();
            _cam.enabled = false;
            _cam.stereoTargetEye = StereoTargetEyeMask.None;
            _cam.depth = -50f;
            _cam.nearClipPlane = 0.12f;
            _cam.farClipPlane = 60000f;
            _cam.clearFlags = CameraClearFlags.Skybox;
            _cam.allowHDR = false;

            _rt = new RenderTexture(TexW, TexH, 24, RenderTextureFormat.ARGB32)
            {
                name = "FPVMod.FeedRT",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear
            };
            _rt.Create();
            _cam.targetTexture = _rt;
        }

        private static void EnsureOverlay()
        {
            if (_overlayGo != null && _raw != null)
                return;

            _overlayGo = new GameObject("FPVMod.FeedOverlay");
            Object.DontDestroyOnLoad(_overlayGo);
            var canvas = _overlayGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30; // below pause menu
            _overlayGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var cg = _overlayGo.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            // No GraphicRaycaster — full-screen feed must not eat pause clicks.

            var imgGo = new GameObject("Feed");
            imgGo.transform.SetParent(_overlayGo.transform, false);
            _raw = imgGo.AddComponent<RawImage>();
            _raw.color = Color.white;
            _raw.raycastTarget = false;
            var rt = _raw.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _overlayGo.SetActive(false);
        }
    }
}
