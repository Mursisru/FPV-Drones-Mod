using System;
using System.Reflection;
using FPVMod.Access;
using HarmonyLib;
using NuclearOption.Effects;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FPVMod.FpvView
{
    /// <summary>
    /// Full MC RenderPrep port: terrain window, DetailRenderer frustum, URP mirror from TargetCam.
    /// Without this, feed looks like "no shaders / no vanilla PP".
    /// </summary>
    internal static class FpvRenderPrep
    {
        private static readonly int WindowDataId = Shader.PropertyToID("_WindowData");
        private static readonly int HeightMapId = Shader.PropertyToID("_HeightMap");
        private static readonly int BlockerMapId = Shader.PropertyToID("_BlockerMap");

        private static readonly FieldInfo? DetailCameraField =
            AccessTools.Field(typeof(DetailRenderer), "camera");
        private static readonly MethodInfo? DetailLateUpdateMethod =
            AccessTools.Method(typeof(DetailRenderer), "LateUpdate");
        private static readonly FieldInfo? RendererIndexField =
            AccessTools.Field(typeof(UniversalAdditionalCameraData), "m_RendererIndex");

        private static Vector2Int _lastBakedWindow = new(int.MinValue, int.MinValue);
        private static CommandBuffer? _terrainWindowCmd;
        private static bool _pipelineHooksRegistered;
        private static Camera? _pipelineFeedCamera;
        private static bool _pipelineForceLdr;
        private static bool _pipelineInfrared;
        private static bool _pipelineNightVision;
        private static bool _pipelineFogPrev;
        private static bool _pipelineFogActive;
        private static int _cachedWindowSize = -1;
        private static int _cachedWindowSnapping = -1;
        private static float _nextWindowCacheTime;
        private const float WindowCacheInterval = 1f;

        private static Camera? _lastMirrorReference;
        private static int _lastMirrorCulling = int.MinValue;
        private static bool _lastMirrorAllowHdr;
        private static bool _lastMirrorAllowMsaa;
        private static CameraClearFlags _lastMirrorClearFlags;
        private static bool _lastMirrorForceLdr;
        private static bool _lastMirrorInfrared;
        private static bool _lastMirrorNightVision;
        private static int _lastMirrorRendererIndex = int.MinValue;
        private static bool _lastMirrorRenderShadows;
        private static AntialiasingMode _lastMirrorAa;
        private static AntialiasingQuality _lastMirrorAaQuality;
        private static bool _lastMirrorDithering;
        private static bool _lastMirrorStopNaN;
        private static int _lastMirrorVolumeLayers = int.MinValue;

        internal static void BeforeRender(Camera feedCamera, bool forceLdr = false)
        {
            ApplyShaderGlobalsForCamera(feedCamera);
            MirrorUrpFromReference(feedCamera, forceLdr);
            BakeTerrainWindowForCamera(feedCamera);
            // FS: force every frame + leave assigned (MC tree flicker fix).
            SyncDetailsToCamera(feedCamera, force: true, leaveAssigned: true);
        }

        /// <summary>FS: keep DetailRenderer on feed frustum — restore only on Detach.</summary>
        internal static void AfterRender()
        {
            // no-op while FS active (MC parity)
        }

        internal static void SetPipelineDriven(
            Camera? feedCamera,
            bool active,
            bool forceLdr = false,
            bool infrared = false,
            bool nightVision = false)
        {
            if (!active || feedCamera == null)
            {
                UnregisterPipelineHooks();
                _pipelineFeedCamera = null;
                _pipelineInfrared = false;
                _pipelineNightVision = false;
                _pipelineForceLdr = false;
                return;
            }

            _pipelineFeedCamera = feedCamera;
            _pipelineForceLdr = forceLdr;
            _pipelineInfrared = infrared;
            _pipelineNightVision = nightVision;
            RegisterPipelineHooks();
            MirrorUrpFromReference(feedCamera, forceLdr);
        }

        internal static void SetPipelineInfrared(bool infrared) => _pipelineInfrared = infrared;
        internal static void SetPipelineNightVision(bool nightVision) => _pipelineNightVision = nightVision;

        internal static int ResolvePipelineMsaaSampleCount()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset)
            {
                int samples = asset.msaaSampleCount;
                if (samples >= 8) return 8;
                if (samples >= 4) return 4;
                if (samples >= 2) return 2;
            }
            return 1;
        }

        internal static void ForceRestoreWorldState()
        {
            Camera? world = ResolveWorldCamera();
            if (world != null)
            {
                try
                {
                    ApplyShaderGlobalsForCamera(world);
                    SyncDetailsToCamera(world, force: true, leaveAssigned: true);
                }
                catch { /* ignore */ }
            }
        }

        internal static void ResetAll()
        {
            UnregisterPipelineHooks();
            _pipelineFeedCamera = null;
            _pipelineInfrared = false;
            _pipelineNightVision = false;
            _pipelineForceLdr = false;
            _lastBakedWindow = new Vector2Int(int.MinValue, int.MinValue);
            InvalidateMirrorCache();
            if (_pipelineFogActive)
            {
                RenderSettings.fog = _pipelineFogPrev;
                _pipelineFogActive = false;
            }
            ForceRestoreWorldState();
        }

        private static void InvalidateMirrorCache()
        {
            _lastMirrorReference = null;
            _lastMirrorCulling = int.MinValue;
            _lastMirrorRendererIndex = int.MinValue;
            _lastMirrorVolumeLayers = int.MinValue;
        }

        private static void RegisterPipelineHooks()
        {
            if (_pipelineHooksRegistered)
                return;
            RenderPipelineManager.beginCameraRendering += OnBegin;
            RenderPipelineManager.endCameraRendering += OnEnd;
            _pipelineHooksRegistered = true;
        }

        private static void UnregisterPipelineHooks()
        {
            if (!_pipelineHooksRegistered)
                return;
            RenderPipelineManager.beginCameraRendering -= OnBegin;
            RenderPipelineManager.endCameraRendering -= OnEnd;
            _pipelineHooksRegistered = false;
        }

        private static void OnBegin(ScriptableRenderContext context, Camera camera)
        {
            if (_pipelineFeedCamera == null || camera != _pipelineFeedCamera)
                return;

            _pipelineFogPrev = RenderSettings.fog;
            _pipelineFogActive = true;
            RenderSettings.fog = !_pipelineInfrared;
            ApplyShaderGlobalsForCamera(camera);
            MirrorUrpFromReference(camera, _pipelineForceLdr);
            BakeTerrainWindowForCamera(camera);
            SyncDetailsToCamera(camera, force: true, leaveAssigned: true);
        }

        private static void OnEnd(ScriptableRenderContext context, Camera camera)
        {
            if (_pipelineFeedCamera == null || camera != _pipelineFeedCamera)
                return;
            if (_pipelineFogActive)
            {
                RenderSettings.fog = _pipelineFogPrev;
                _pipelineFogActive = false;
            }
        }

        private static void SyncDetailsToCamera(Camera targetCamera, bool force, bool leaveAssigned)
        {
            if (targetCamera == null || DetailCameraField == null || DetailLateUpdateMethod == null)
                return;

            DetailRenderer? detail = null;
            try { detail = SceneSingleton<DetailRenderer>.i; }
            catch { return; }
            if (detail == null || !detail.isActiveAndEnabled)
                return;

            object? previous = DetailCameraField.GetValue(detail);
            try
            {
                DetailCameraField.SetValue(detail, targetCamera);
                DetailLateUpdateMethod.Invoke(detail, null);
            }
            catch (Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning("FPV detail sync: " + ex.Message);
            }
            finally
            {
                if (!leaveAssigned)
                {
                    try { DetailCameraField.SetValue(detail, previous); }
                    catch { /* ignore */ }
                }
            }

            _ = force;
        }

        private static void ApplyShaderGlobalsForCamera(Camera camera)
        {
            int maxTargetOffset = GetMaxTargetOffset();
            ShaderGlobalManager.SetCameraPlanes(camera, maxTargetOffset, out _);

            Vector2Int windowIndex = GetWindowIndex(camera.transform.position);
            Shader.SetGlobalVector(
                WindowDataId,
                new Vector4(windowIndex.x, windowIndex.y, GetWindowSnapping(), GetWindowSize()));
        }

        private static void BakeTerrainWindowForCamera(Camera camera)
        {
            TerrainHeightMap? terrainHeightMap = SceneSingleton<TerrainHeightMap>.i;
            if (terrainHeightMap == null || terrainHeightMap.heightMap == null)
                return;

            Vector2Int windowIndex = GetWindowIndex(camera.transform.position);
            if (windowIndex != _lastBakedWindow)
            {
                _lastBakedWindow = windowIndex;
                if (_terrainWindowCmd == null)
                    _terrainWindowCmd = new CommandBuffer { name = "FPV.TerrainWindow" };
                else
                    _terrainWindowCmd.Clear();

                terrainHeightMap.BakeWindow(_terrainWindowCmd, windowIndex);
                Graphics.ExecuteCommandBuffer(_terrainWindowCmd);
            }

            Shader.SetGlobalTexture(HeightMapId, terrainHeightMap.heightMap);
            Shader.SetGlobalTexture(BlockerMapId, terrainHeightMap.blockerMap);
        }

        private static void MirrorUrpFromReference(Camera feedCamera, bool forceLdr)
        {
            Camera? reference = ResolveReferenceCamera() ?? ResolveWorldCamera();
            if (reference == null)
                return;

            UniversalAdditionalCameraData feedUrp = feedCamera.GetUniversalAdditionalCameraData();
            UniversalAdditionalCameraData refUrp = reference.GetUniversalAdditionalCameraData();
            int rendererIndex = GetRendererIndex(refUrp);
            bool wantHdr = !forceLdr && reference.allowHDR;
            // COLOR pipeline: world URP volumes. IR blit: off. NVG/IR volume: on.
            bool wantPp = _pipelineInfrared
                || _pipelineNightVision
                || _pipelineFeedCamera != null;
            // MC: reference | Effects | TransparentFX. FPV also Water|Sun.
            int desiredCulling = reference.cullingMask
                | (int)PhysicsLayers.EffectsMask
                | (int)PhysicsLayers.TransparentFXMask
                | (int)PhysicsLayers.WaterMask
                | (int)PhysicsLayers.SunMask;
            int desiredVolumeLayers = ResolveVolumeLayerMask(refUrp);

            bool dirty = !ReferenceEquals(reference, _lastMirrorReference)
                || _lastMirrorCulling != desiredCulling
                || _lastMirrorAllowHdr != wantHdr
                || _lastMirrorAllowMsaa != reference.allowMSAA
                || _lastMirrorClearFlags != reference.clearFlags
                || _lastMirrorForceLdr != forceLdr
                || _lastMirrorInfrared != _pipelineInfrared
                || _lastMirrorNightVision != _pipelineNightVision
                || _lastMirrorRendererIndex != rendererIndex
                || _lastMirrorRenderShadows != refUrp.renderShadows
                || _lastMirrorAa != refUrp.antialiasing
                || _lastMirrorAaQuality != refUrp.antialiasingQuality
                || _lastMirrorDithering != refUrp.dithering
                || _lastMirrorStopNaN != refUrp.stopNaN
                || _lastMirrorVolumeLayers != desiredVolumeLayers
                || feedUrp.renderPostProcessing != wantPp
                || feedCamera.cullingMask != desiredCulling;

            if (!dirty)
            {
                feedUrp.volumeTrigger = feedCamera.transform;
                return;
            }

            feedCamera.cullingMask = desiredCulling;
            feedCamera.allowHDR = wantHdr;
            feedCamera.allowMSAA = reference.allowMSAA;
            feedCamera.clearFlags = reference.clearFlags;

            feedUrp.SetRenderer(rendererIndex);
            feedUrp.renderShadows = refUrp.renderShadows;
            feedUrp.renderPostProcessing = wantPp;
            feedUrp.volumeTrigger = feedCamera.transform;
            feedUrp.volumeLayerMask = desiredVolumeLayers;
            feedUrp.antialiasing = refUrp.antialiasing;
            feedUrp.antialiasingQuality = refUrp.antialiasingQuality;
            feedUrp.dithering = refUrp.dithering;
            feedUrp.stopNaN = refUrp.stopNaN;
            feedUrp.requiresDepthOption = CameraOverrideOption.UsePipelineSettings;
            feedUrp.requiresColorOption = CameraOverrideOption.UsePipelineSettings;

            _lastMirrorReference = reference;
            _lastMirrorCulling = desiredCulling;
            _lastMirrorAllowHdr = wantHdr;
            _lastMirrorAllowMsaa = reference.allowMSAA;
            _lastMirrorClearFlags = reference.clearFlags;
            _lastMirrorForceLdr = forceLdr;
            _lastMirrorInfrared = _pipelineInfrared;
            _lastMirrorNightVision = _pipelineNightVision;
            _lastMirrorRendererIndex = rendererIndex;
            _lastMirrorRenderShadows = refUrp.renderShadows;
            _lastMirrorAa = refUrp.antialiasing;
            _lastMirrorAaQuality = refUrp.antialiasingQuality;
            _lastMirrorDithering = refUrp.dithering;
            _lastMirrorStopNaN = refUrp.stopNaN;
            _lastMirrorVolumeLayers = desiredVolumeLayers;
        }

        /// <summary>World PP volumes live on PP layer; TargetCam may only expose TargetCamPP.</summary>
        private static int ResolveVolumeLayerMask(UniversalAdditionalCameraData refUrp)
        {
            int mask = (int)refUrp.volumeLayerMask
                | (int)PhysicsLayers.PPMask
                | (int)PhysicsLayers.DefaultMask;

            Camera? world = ResolveWorldCamera();
            if (world != null)
            {
                try
                {
                    mask |= (int)world.GetUniversalAdditionalCameraData().volumeLayerMask;
                }
                catch { /* ignore */ }
            }

            return mask;
        }

        /// <summary>Prefer vanilla TargetCam (same AA/shadows/renderer as cockpit seeker).</summary>
        private static Camera? ResolveReferenceCamera()
        {
            Camera? target = FpvTargetCamAccess.TryGetLocalTargetCam();
            if (target != null)
                return target;
            return null;
        }

        private static Camera? ResolveWorldCamera()
        {
            Camera? main = Camera.main;
            if (main != null)
                return main;

            try
            {
                CameraStateManager? csm = SceneSingleton<CameraStateManager>.i;
                if (csm != null && csm.mainCamera != null)
                    return csm.mainCamera;
            }
            catch { /* ignore */ }

            Camera[] cams = Camera.allCameras;
            for (int i = 0; i < cams.Length; i++)
            {
                Camera c = cams[i];
                if (c != null && c.enabled && c.targetTexture == null)
                    return c;
            }
            return null;
        }

        private static int GetRendererIndex(UniversalAdditionalCameraData cameraData)
        {
            if (RendererIndexField?.GetValue(cameraData) is int index)
                return index;
            return 0;
        }

        private static int GetMaxTargetOffset()
        {
            EnsureWindowCache();
            return Mathf.Max(0, _cachedWindowSize / 2 - _cachedWindowSnapping * 2);
        }

        private static int GetWindowSize()
        {
            EnsureWindowCache();
            return _cachedWindowSize;
        }

        private static int GetWindowSnapping()
        {
            EnsureWindowCache();
            return _cachedWindowSnapping;
        }

        private static void EnsureWindowCache()
        {
            float now = Time.unscaledTime;
            if (_cachedWindowSize > 0 && now < _nextWindowCacheTime)
                return;

            _nextWindowCacheTime = now + WindowCacheInterval;
            DetailRenderer? detail = null;
            try { detail = SceneSingleton<DetailRenderer>.i; }
            catch { /* keep previous */ }

            _cachedWindowSize = detail != null ? detail.windowSize : 1024;
            _cachedWindowSnapping = detail != null ? detail.windowSnapping : 64;
        }

        private static Vector2Int GetWindowIndex(Vector3 localPosition)
        {
            GlobalPosition global = localPosition.ToGlobalPosition();
            int snapping = Mathf.Max(1, GetWindowSnapping());
            return new Vector2Int(
                Mathf.FloorToInt(global.x / snapping),
                Mathf.FloorToInt(global.z / snapping));
        }
    }
}
