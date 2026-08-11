using FPVMod.Effects;
using FPVMod.Hud;
using FPVMod.Session;
using FPVMod.Ui;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace FPVMod.FpvView
{
    /// <summary>
    /// Owned FS feed — MC Rig + FeedController parity (no MC.dll).
    /// Default vision WH. Manual IR/NVG: Overlay idle + one Base Camera.Render.
    /// </summary>
    internal static class FpvFeedCamera
    {
        private const float NoseLocalZ = 1.2f;
        private const int TexW = 1920;
        private const int TexH = 1080;
        private const float ZoomWheelFactor = 1.12f;
        private const float MfdMaxFov = 120f;
        private const float NvgGainMax = 3f;
        private const float NvgGainMin = 0.5f;
        private const float NvgBloomThresholdMin = 0.2f;
        private const float NvgBloomThresholdMax = 1.2f;
        private const float ExposureEpsilon = 0.0005f;

        private static GameObject? _rigGo;
        private static Camera? _cam;
        private static RenderTexture? _rt;
        private static RenderTexture? _hdrRt;
        private static GameObject? _overlayGo;
        private static RectTransform? _panelRt;
        private static RawImage? _raw;
        private static FpvGunshipHud? _gunship;
        private static Image? _staticOverlay;
        private static Missile? _missile;
        private static bool _active;
        private static float _magnification = 1f;
        private static bool _pipelineDriven = true;
        private static FpvVisionMode _armedMode = (FpvVisionMode)255;
        private static float _blitExposure = 1.15f;
        private static float _blitContrast = 1f;
        private static float _lastPolicyExposure = float.NaN;

        private static Volume? _irVolume;
        private static ColorAdjustments? _colorAdj;
        private static Bloom? _bloom;
        private static float _nvgGainStamp = -1f;

        internal static bool IsActive => _active && _missile != null;
        internal static bool UseIdleDriverWait => !_active;
        internal static float Magnification => _magnification;
        internal static Camera? FeedCamera => _cam;
        internal static RectTransform? PanelRt => _panelRt;

        internal static void Attach(Missile drone)
        {
            if (drone == null)
                return;

            Detach();
            FpvFeedDriverHost.Ensure();
            FpvShaderBundle.EnsureLoaded();
            EnsureRig();
            EnsureOverlay();
            EnsureVolume();
            if (_rigGo == null || _cam == null || _raw == null)
                return;

            _missile = drone;
            _magnification = 1f;
            FpvVisionModeController.Reset();
            FpvInfraredPolicy.Reset();
            _armedMode = (FpvVisionMode)255;
            _lastPolicyExposure = float.NaN;
            _rigGo.transform.SetParent(drone.transform, false);
            RebuildDisplayRt();
            ApplyPose();
            ApplyEffectiveFov();
            _cam.useOcclusionCulling = false;

            if (_overlayGo != null)
                _overlayGo.SetActive(true);
            _gunship?.SetVisible(true);
            _gunship?.SetTvOverlayEnabled(FpvConfig.FxScanlinesEnabled.Value);
            _active = true;

            FpvLookAroundHud.BindParent(_panelRt);
            ApplyVisionPath(force: true);

            try { FlightHud.EnableCanvas(false); } catch { /* ignore */ }
            FpvPlugin.ModLogger?.LogInfo(
                $"FPV FS attach vision={FpvVisionModeController.Mode} blit={FpvInfraredBlit.IsAvailable} msaa={_rt?.antiAliasing}");
        }

        /// <summary>Keys in Update (MC PollInputEarly) — not EOF.</summary>
        internal static void PollInputEarly()
        {
            if (!_active || _missile == null)
                return;
            if (FpvUiGate.BlocksFlightInput)
            {
                FpvFsLookAround.Reset();
                return;
            }

            FpvVisionModeController.TickInput(FpvControlSession.Active);
            FpvFsLookAround.Tick(FpvControlSession.Active);
            ProcessZoom();
        }

        /// <summary>EOF render + HUD (MC FeedController.Tick).</summary>
        internal static void TickEndOfFrame()
        {
            if (!_active || _missile == null || _missile.disabled || _rigGo == null || _cam == null)
                return;

            ApplyPose();
            ApplyEffectiveFov();
            SyncDisplayFilter();

            float policyExp = FpvInfraredPolicy.Evaluate(_missile.transform.position);
            ApplyVisionPath(force: false, policyExp);

            if (!_pipelineDriven)
                ManualRenderFrame();

            if (_gunship != null)
            {
                FpvGunshipSnapshot snap = FpvGunshipSnapshot.Build(_missile, _cam.fieldOfView);
                _gunship.Update(snap, FpvPanelMetrics.FromRect(_panelRt));
                _gunship.SetTvOverlayEnabled(FpvConfig.FxScanlinesEnabled.Value);
            }

            BindDisplayTexture();
        }

        /// <summary>Legacy name — host no longer calls this; driver owns timing.</summary>
        internal static void LateTick() { /* moved to TickEndOfFrame */ }

        internal static void TickPauseUi()
        {
            if (_overlayGo == null || !_active)
                return;
            var cg = _overlayGo.GetComponent<CanvasGroup>();
            if (cg != null)
                cg.alpha = FpvUiGate.MenuOpen ? 0.15f : 1f;
        }

        internal static void SetLinkStatic(float alpha01)
        {
            if (_staticOverlay == null)
                return;
            _staticOverlay.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha01));
        }

        internal static void Detach()
        {
            _active = false;
            _missile = null;
            if (FpvConfig.ZoomResetOnExit.Value)
                _magnification = 1f;

            FpvFsLookAround.Reset();
            FpvLookAroundHud.DestroyUi();
            FpvVisionModeController.Reset();
            FpvInfraredPolicy.Reset();
            DisableNightVisionVolume();
            FpvRenderPrep.SetPipelineDriven(null, false);
            FpvRenderPrep.ResetAll();
            FpvPostFxStack.Release();
            _pipelineDriven = true;
            _armedMode = (FpvVisionMode)255;
            _lastPolicyExposure = float.NaN;

            if (_cam != null)
            {
                try
                {
                    var urp = _cam.GetUniversalAdditionalCameraData();
                    urp.renderType = CameraRenderType.Base;
                    urp.renderPostProcessing = false;
                }
                catch { /* ignore */ }
                _cam.enabled = false;
                _cam.targetTexture = _rt;
            }
            if (_rigGo != null && _rigGo.transform.parent != null)
                _rigGo.transform.SetParent(null, true);
            _gunship?.SetVisible(false);
            SetLinkStatic(0f);
            if (_overlayGo != null)
                _overlayGo.SetActive(false);
            try
            {
                var csm = SceneSingleton<CameraStateManager>.i;
                FlightHud.EnableCanvas(csm != null && csm.currentState == csm.cockpitState);
            }
            catch { /* ignore */ }
        }

        private static void ApplyVisionPath(bool force, float policyExposure = float.NaN)
        {
            FpvVisionMode mode = FpvVisionModeController.Mode;
            bool needManual = FpvVisionModeController.UsesInfraredBlit(mode)
                || FpvVisionModeController.UsesNightVisionVolume(mode);

            bool exposureDirty = FpvVisionModeController.UsesInfraredBlit(mode)
                && !float.IsNaN(policyExposure)
                && (float.IsNaN(_lastPolicyExposure)
                    || Mathf.Abs(_lastPolicyExposure - policyExposure) > ExposureEpsilon);

            if (!force && mode == _armedMode && needManual == !_pipelineDriven && !exposureDirty)
                return;

            _armedMode = mode;
            if (!float.IsNaN(policyExposure))
                _lastPolicyExposure = policyExposure;

            if (FpvVisionModeController.UsesInfraredBlit(mode))
            {
                float policy = float.IsNaN(policyExposure) ? FpvInfraredPolicy.Exposure : policyExposure;
                ResolveBlitParams(policy);
                DisableNightVisionVolume();
                FpvRenderPrep.SetPipelineDriven(null, false);
                FpvRenderPrep.SetPipelineInfrared(false);
                FpvRenderPrep.SetPipelineNightVision(false);
                _pipelineDriven = false;
                ApplyFeedCameraActiveState();
            }
            else if (FpvVisionModeController.UsesNightVisionVolume(mode))
            {
                EnableNightVisionVolume();
                FpvRenderPrep.SetPipelineDriven(null, false);
                FpvRenderPrep.SetPipelineInfrared(false);
                FpvRenderPrep.SetPipelineNightVision(true);
                _pipelineDriven = false;
                ApplyFeedCameraActiveState();
            }
            else
            {
                DisableNightVisionVolume();
                _pipelineDriven = true;
                ApplyFeedCameraActiveState();
                FpvRenderPrep.SetPipelineDriven(
                    _cam, true, forceLdr: false, infrared: false, nightVision: false);
            }
        }

        private static void ResolveBlitParams(float policyExposure)
        {
            float contrast = 1f;
            if (Access.FpvTargetCamAccess.TryGetVanillaIrSnapshot(out bool vanillaIr, out _, out float vanillaContrast)
                && vanillaIr)
                contrast = vanillaContrast;

            _blitExposure = FpvInfraredExposure.Resolve(policyExposure, out _);
            _blitContrast = contrast;
        }

        private static void ApplyFeedCameraActiveState()
        {
            if (_cam == null)
                return;

            UniversalAdditionalCameraData urp = _cam.GetUniversalAdditionalCameraData();
            if (_pipelineDriven)
            {
                urp.renderType = CameraRenderType.Base;
                urp.renderPostProcessing = false;
                _cam.targetTexture = _rt;
                _cam.enabled = true;
                return;
            }

            urp.renderType = CameraRenderType.Overlay;
            urp.renderPostProcessing = false;
            _cam.targetTexture = _rt;
            _cam.enabled = false;
        }

        private static void ManualRenderFrame()
        {
            if (_cam == null || _rt == null)
                return;

            FpvVisionMode mode = FpvVisionModeController.Mode;
            bool useBlit = FpvVisionModeController.UsesInfraredBlit(mode);
            bool useNvg = FpvVisionModeController.UsesNightVisionVolume(mode);

            UniversalAdditionalCameraData urp = _cam.GetUniversalAdditionalCameraData();
            CameraRenderType prevType = urp.renderType;
            bool prevHdr = _cam.allowHDR;
            bool prevFog = RenderSettings.fog;
            RenderTexture? prevActive = RenderTexture.active;

            try
            {
                RenderSettings.fog = !useBlit;

                if (useBlit)
                {
                    EnsureHdr();
                    _cam.allowHDR = true;
                    _cam.targetTexture = _hdrRt;
                    urp.renderPostProcessing = false;
                    if (_irVolume != null)
                        _irVolume.enabled = false;
                    FpvRenderPrep.BeforeRender(_cam, forceLdr: false);
                }
                else if (useNvg)
                {
                    TickNvgGain();
                    _cam.allowHDR = true;
                    _cam.targetTexture = _rt;
                    if (_irVolume != null)
                    {
                        _irVolume.isGlobal = true;
                        _irVolume.enabled = true;
                    }
                    urp.renderPostProcessing = true;
                    urp.volumeTrigger = _cam.transform;
                    FpvRenderPrep.BeforeRender(_cam, forceLdr: false);
                }
                else
                {
                    _cam.targetTexture = _rt;
                    urp.renderPostProcessing = false;
                    FpvRenderPrep.BeforeRender(_cam, forceLdr: false);
                }

                urp.renderType = CameraRenderType.Base;
                _cam.enabled = true;
                _cam.Render();

                if (useBlit && _hdrRt != null)
                    FpvInfraredBlit.Apply(_hdrRt, _rt, _blitExposure, _blitContrast, mode);
            }
            finally
            {
                RenderSettings.fog = prevFog;
                RenderTexture.active = prevActive;
                urp.renderPostProcessing = false;
                if (_irVolume != null)
                {
                    _irVolume.enabled = false;
                    _irVolume.isGlobal = false;
                }
                _cam.allowHDR = prevHdr;
                _cam.targetTexture = _rt;
                urp.renderType = prevType;
                FpvRenderPrep.AfterRender();
                ApplyFeedCameraActiveState();
            }
        }

        private static void EnsureHdr()
        {
            if (_hdrRt != null && _hdrRt.width == TexW && _hdrRt.height == TexH)
                return;
            if (_hdrRt != null)
            {
                _hdrRt.Release();
                Object.Destroy(_hdrRt);
            }
            _hdrRt = new RenderTexture(TexW, TexH, 16, RenderTextureFormat.ARGBHalf)
            {
                name = "FPVMod.FeedHDR",
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                hideFlags = HideFlags.HideAndDontSave
            };
            _hdrRt.Create();
        }

        private static void RebuildDisplayRt()
        {
            if (_cam == null)
                return;

            int msaa = FpvRenderPrep.ResolvePipelineMsaaSampleCount();
            bool bilinear = _magnification > 1.01f;
            if (_rt != null
                && _rt.width == TexW
                && _rt.height == TexH
                && _rt.antiAliasing == msaa)
            {
                _rt.filterMode = bilinear ? FilterMode.Bilinear : FilterMode.Point;
                _cam.targetTexture = _rt;
                return;
            }

            if (_rt != null)
            {
                _cam.targetTexture = null;
                _rt.Release();
                Object.Destroy(_rt);
            }

            _rt = new RenderTexture(TexW, TexH, 16, RenderTextureFormat.ARGB32)
            {
                name = "FPVMod.FeedRT",
                antiAliasing = msaa,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = bilinear ? FilterMode.Bilinear : FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            _rt.Create();
            _cam.targetTexture = _rt;
            if (_hdrRt != null)
            {
                _hdrRt.Release();
                Object.Destroy(_hdrRt);
                _hdrRt = null;
            }
        }

        private static void SyncDisplayFilter()
        {
            if (_rt == null)
                return;
            bool bilinear = _magnification > 1.01f;
            FilterMode want = bilinear ? FilterMode.Bilinear : FilterMode.Point;
            if (_rt.filterMode != want)
                _rt.filterMode = want;
        }

        private static void EnsureVolume()
        {
            if (_rigGo == null || _irVolume != null)
                return;

            _irVolume = _rigGo.AddComponent<Volume>();
            _irVolume.isGlobal = false;
            _irVolume.enabled = false;
            _irVolume.priority = 1000f;
            _irVolume.blendDistance = 0f;
            _irVolume.weight = 1f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            _irVolume.profile = profile;
            _colorAdj = profile.Add<ColorAdjustments>(true);
            _bloom = profile.Add<Bloom>(true);
            DisableNightVisionVolume();
        }

        private static void EnableNightVisionVolume()
        {
            if (_colorAdj == null || _bloom == null)
                return;
            _colorAdj.saturation.overrideState = false;
            _colorAdj.contrast.Override(5f);
            _colorAdj.contrast.overrideState = true;
            _colorAdj.colorFilter.Override(new Color(0.55f, 1f, 0.55f, 1f));
            _colorAdj.colorFilter.overrideState = true;
            _bloom.intensity.Override(0.35f);
            _bloom.intensity.overrideState = true;
            _bloom.threshold.overrideState = true;
            TickNvgGain();
        }

        private static void DisableNightVisionVolume()
        {
            if (_bloom != null)
            {
                _bloom.threshold.overrideState = false;
                _bloom.intensity.overrideState = false;
            }
            if (_colorAdj != null)
            {
                _colorAdj.contrast.overrideState = false;
                _colorAdj.postExposure.overrideState = false;
                _colorAdj.colorFilter.overrideState = false;
                _colorAdj.saturation.overrideState = false;
            }
            if (_irVolume != null)
            {
                _irVolume.enabled = false;
                _irVolume.isGlobal = false;
            }
        }

        private static void TickNvgGain()
        {
            if (_colorAdj == null || _bloom == null)
                return;
            if (Time.unscaledTime - _nvgGainStamp < 0.25f)
                return;
            _nvgGainStamp = Time.unscaledTime;

            float ambient = FpvInfraredPolicy.CachedAmbient;
            if (ambient <= 0f)
            {
                try { ambient = RenderSettings.ambientIntensity; }
                catch { ambient = 0.2f; }
            }

            float t = Mathf.InverseLerp(0.01f, 0.4f, ambient);
            _colorAdj.postExposure.Override(Mathf.Lerp(NvgGainMax, NvgGainMin, t));
            _colorAdj.postExposure.overrideState = true;
            _bloom.threshold.Override(Mathf.Lerp(NvgBloomThresholdMin, NvgBloomThresholdMax, t));
        }

        private static void ProcessZoom()
        {
            if (UnityEngine.Input.GetMouseButton(1))
                return;
            if (UnityEngine.Input.GetMouseButtonDown(2))
            {
                _magnification = 1f;
                return;
            }

            float scroll = UnityEngine.Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.01f)
                return;

            float mul = scroll > 0f ? ZoomWheelFactor : 1f / ZoomWheelFactor;
            float maxMag = Mathf.Clamp(FpvConfig.ZoomMax.Value, 2f, 50f);
            _magnification = Mathf.Clamp(_magnification * mul, 1f, maxMag);
        }

        private static void ApplyEffectiveFov()
        {
            if (_cam == null)
                return;
            float maxMag = Mathf.Clamp(FpvConfig.ZoomMax.Value, 2f, 50f);
            float mag = Mathf.Clamp(_magnification, 1f, maxMag);
            float safeBase = Mathf.Max(FpvConstants.CameraFov, 1f);
            float minFov = safeBase / maxMag;
            float maxFov = Mathf.Min(MfdMaxFov, safeBase);
            _cam.fieldOfView = Mathf.Clamp(safeBase / mag, minFov, maxFov);
        }

        private static void BindDisplayTexture()
        {
            if (_raw == null || _rt == null)
                return;

            // MC: PostFx Scanlines stage hard-off; MB/CA/Bloom default off → usually pass-through.
            RenderTexture shown = FpvPostFxStack.Apply(_rt) ?? _rt;
            if (_raw.texture != shown)
                _raw.texture = shown;
        }

        private static void ApplyPose()
        {
            if (_rigGo == null)
                return;
            _rigGo.transform.localPosition = new Vector3(0f, 0.05f, NoseLocalZ);
            _rigGo.transform.localRotation = Quaternion.Euler(FpvConstants.CameraPitchDeg, 0f, 0f);
            FpvFsLookAround.ApplyToCamera(_cam);
        }

        private static void EnsureRig()
        {
            if (_rigGo != null && _cam != null)
                return;

            _rigGo = new GameObject("FPVMod.FeedCam");
            Object.DontDestroyOnLoad(_rigGo);
            _cam = _rigGo.AddComponent<Camera>();
            _cam.enabled = false;
            _cam.stereoTargetEye = StereoTargetEyeMask.None;
            _cam.depth = -100f;
            _cam.nearClipPlane = 0.15f;
            _cam.farClipPlane = 60000f;
            _cam.clearFlags = CameraClearFlags.Skybox;
            _cam.allowHDR = true;
            _cam.useOcclusionCulling = false;

            try
            {
                var urp = _cam.GetUniversalAdditionalCameraData();
                urp.renderType = CameraRenderType.Base;
                urp.requiresColorOption = CameraOverrideOption.UsePipelineSettings;
                urp.requiresDepthOption = CameraOverrideOption.UsePipelineSettings;
                urp.volumeTrigger = _cam.transform;
            }
            catch { /* ignore */ }
        }

        private static void EnsureOverlay()
        {
            if (_overlayGo != null && _raw != null && _gunship != null && _panelRt != null)
                return;

            if (_overlayGo == null)
            {
                _overlayGo = new GameObject("FPVMod.GameFullscreen");
                Object.DontDestroyOnLoad(_overlayGo);
                var canvas = _overlayGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50;
                canvas.overrideSorting = true;
                var scaler = _overlayGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                var cg = _overlayGo.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = false;
                cg.interactable = false;
            }

            if (_panelRt == null)
            {
                var rootGo = new GameObject("FullscreenRoot", typeof(RectTransform));
                rootGo.transform.SetParent(_overlayGo!.transform, false);
                GunshipChrome.Stretch(rootGo.GetComponent<RectTransform>());

                var backGo = new GameObject("FullscreenBackdrop", typeof(RectTransform), typeof(Image));
                backGo.transform.SetParent(rootGo.transform, false);
                GunshipChrome.Stretch(backGo.GetComponent<RectTransform>());
                FpvUiImageHelper.ApplySolid(backGo.GetComponent<Image>(), Color.black);

                var panelGo = new GameObject("FullscreenPanel", typeof(RectTransform));
                panelGo.transform.SetParent(rootGo.transform, false);
                _panelRt = panelGo.GetComponent<RectTransform>();
                GunshipChrome.Stretch(_panelRt);
            }

            if (_raw == null)
            {
                var imgGo = new GameObject("Feed", typeof(RectTransform));
                imgGo.transform.SetParent(_panelRt, false);
                imgGo.transform.SetAsFirstSibling();
                _raw = imgGo.AddComponent<RawImage>();
                _raw.color = Color.white;
                _raw.raycastTarget = false;
                GunshipChrome.Stretch(_raw.rectTransform);
            }

            if (_staticOverlay == null)
            {
                var stGo = new GameObject("LinkStatic", typeof(RectTransform));
                stGo.transform.SetParent(_panelRt, false);
                _staticOverlay = stGo.AddComponent<Image>();
                _staticOverlay.color = Color.clear;
                _staticOverlay.raycastTarget = false;
                GunshipChrome.Stretch(_staticOverlay.rectTransform);
            }

            if (_gunship == null)
                _gunship = FpvGunshipHud.Create(_panelRt!);

            _overlayGo!.SetActive(false);
        }
    }
}
