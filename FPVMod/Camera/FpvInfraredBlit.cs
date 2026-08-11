using FPVMod.Effects;
using UnityEngine;

namespace FPVMod.FpvView
{
    /// <summary>HDR → WH/BH/EDGE blit (MC InfraredBlit port).</summary>
    internal static class FpvInfraredBlit
    {
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int ContrastId = Shader.PropertyToID("_Contrast");
        private static readonly int HighlightCompressId = Shader.PropertyToID("_HighlightCompress");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int EdgeStrengthId = Shader.PropertyToID("_EdgeStrength");

        private const float HighlightCompress = 0.35f;
        private const float EdgeStrength = 2.5f;
        private const float BlackHotExposureBias = -0.75f;

        private static Material? _material;
        private static bool _initFailed;
        private static bool _logged;
        private static float _lastExposure = float.NaN;
        private static float _lastContrast = float.NaN;
        private static int _lastMode = int.MinValue;

        internal static bool IsAvailable
        {
            get
            {
                EnsureMaterial();
                return _material != null;
            }
        }

        internal static void Apply(
            RenderTexture source,
            RenderTexture destination,
            float exposure,
            float contrast,
            FpvVisionMode mode)
        {
            if (source == null || destination == null)
                return;

            Material? mat = EnsureMaterial();
            if (mat == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            float applyExposure = mode == FpvVisionMode.BlackHot
                ? exposure + BlackHotExposureBias
                : exposure;

            Sync(mat, applyExposure, contrast, mode);
            Graphics.Blit(source, destination, mat);

            if (!_logged)
            {
                _logged = true;
                FpvPlugin.ModLogger?.LogInfo("FPV vision blit ready: " + mat.shader.name);
            }
        }

        internal static void Shutdown()
        {
            if (_material != null)
            {
                Object.Destroy(_material);
                _material = null;
            }
            _initFailed = false;
            _logged = false;
            _lastExposure = float.NaN;
            _lastContrast = float.NaN;
            _lastMode = int.MinValue;
        }

        private static Material? EnsureMaterial()
        {
            if (_material != null)
            {
                Shader? live = _material.shader;
                if (live != null && live.isSupported
                    && live.name.IndexOf("Error", System.StringComparison.OrdinalIgnoreCase) < 0)
                    return _material;
                Object.Destroy(_material);
                _material = null;
                _initFailed = false;
            }

            if (_initFailed)
                return null;

            Shader? shader = FpvShaderBundle.InfraredBlitShader;
            if (shader == null || !shader.isSupported
                || shader.name.IndexOf("Error", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _initFailed = true;
                FpvPlugin.ModLogger?.LogWarning("FPV: InfraredBlit shader missing.");
                return null;
            }

            try
            {
                _material = new Material(shader)
                {
                    name = "FPV.VisionBlit",
                    hideFlags = HideFlags.HideAndDontSave
                };
                return _material;
            }
            catch (System.Exception ex)
            {
                _initFailed = true;
                FpvPlugin.ModLogger?.LogWarning("FPV vision blit material: " + ex.Message);
                return null;
            }
        }

        private static void Sync(Material material, float exposure, float contrast, FpvVisionMode mode)
        {
            int modeInt = (int)mode;
            if (Mathf.Approximately(exposure, _lastExposure)
                && Mathf.Approximately(contrast, _lastContrast)
                && modeInt == _lastMode)
                return;

            _lastExposure = exposure;
            _lastContrast = contrast;
            _lastMode = modeInt;
            material.SetFloat(ExposureId, exposure);
            material.SetFloat(ContrastId, Mathf.Max(0.01f, contrast));
            material.SetFloat(HighlightCompressId, HighlightCompress);
            material.SetFloat(ModeId, modeInt);
            material.SetFloat(EdgeStrengthId, EdgeStrength);
        }
    }
}
