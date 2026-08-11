using UnityEngine;

namespace FPVMod.Effects
{
    /// <summary>
    /// MC PostFxStack parity: Scanlines blit hard-off (CRT = GunshipTvOverlay UI).
    /// MB / Chromatic / Bloom only when config enabled (defaults false).
    /// </summary>
    internal static class FpvPostFxStack
    {
        private static RenderTexture? _tempA;
        private static RenderTexture? _tempB;
        private static int _tempW;
        private static int _tempH;

        private static Material? _blurMat;
        private static Material? _chromaMat;
        private static Material? _bloomMat;
        private static bool _blurFail;
        private static bool _chromaFail;
        private static bool _bloomFail;

        internal static RenderTexture? Apply(RenderTexture? source)
        {
            if (source == null)
                return null;

            bool any = FpvConfig.FxMotionBlurEnabled.Value
                || FpvConfig.FxChromaticEnabled.Value
                || FpvConfig.FxBloomEnabled.Value;
            if (!any)
                return source;

            EnsureTemps(source.width, source.height);
            if (_tempA == null || _tempB == null)
                return source;

            RenderTexture read = source;
            RenderTexture write = _tempA;
            bool wrote = false;

            if (FpvConfig.FxMotionBlurEnabled.Value
                && FpvFxBlit.TryBlit(
                    "Hidden/MissileCamera/MotionBlur",
                    "FPV.MotionBlur",
                    ref _blurMat,
                    ref _blurFail,
                    read,
                    write,
                    intensity: 0.25f))
            {
                wrote = true;
                read = write;
                write = read == _tempA ? _tempB : _tempA;
            }

            if (FpvConfig.FxChromaticEnabled.Value
                && FpvFxBlit.TryBlit(
                    "Hidden/MissileCamera/ChromaticAberration",
                    "FPV.Chromatic",
                    ref _chromaMat,
                    ref _chromaFail,
                    read,
                    write,
                    intensity: 0.2f))
            {
                wrote = true;
                read = write;
                write = read == _tempA ? _tempB : _tempA;
            }

            if (FpvConfig.FxBloomEnabled.Value
                && FpvFxBlit.TryBlit(
                    "Hidden/MissileCamera/Bloom",
                    "FPV.Bloom",
                    ref _bloomMat,
                    ref _bloomFail,
                    read,
                    write,
                    intensity: 0.3f))
            {
                wrote = true;
                read = write;
            }

            return wrote ? read : source;
        }

        internal static void Release()
        {
            ReleaseTemp(ref _tempA);
            ReleaseTemp(ref _tempB);
            _tempW = 0;
            _tempH = 0;
        }

        private static void EnsureTemps(int width, int height)
        {
            if (_tempA != null && _tempB != null && _tempW == width && _tempH == height)
                return;
            Release();
            _tempW = width;
            _tempH = height;
            _tempA = CreateTemp(width, height, "FPV.FxTempA");
            _tempB = CreateTemp(width, height, "FPV.FxTempB");
        }

        private static RenderTexture CreateTemp(int width, int height, string name)
        {
            var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            rt.Create();
            return rt;
        }

        private static void ReleaseTemp(ref RenderTexture? rt)
        {
            if (rt == null)
                return;
            rt.Release();
            Object.Destroy(rt);
            rt = null;
        }
    }
}
