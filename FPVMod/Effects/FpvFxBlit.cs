using UnityEngine;

namespace FPVMod.Effects
{
    internal static class FpvFxBlit
    {
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        internal static bool TryBlit(
            string shaderFindName,
            string materialName,
            ref Material? material,
            ref bool initFailed,
            RenderTexture source,
            RenderTexture destination,
            float intensity,
            float fisheye = 0.14f)
        {
            if (source == null || destination == null)
                return false;

            Material? mat = EnsureMaterial(shaderFindName, materialName, ref material, ref initFailed);
            if (mat == null)
                return false;

            mat.SetFloat(IntensityId, Mathf.Clamp01(intensity));
            if (mat.HasProperty("_LineDensity"))
                mat.SetFloat("_LineDensity", 720f);
            if (mat.HasProperty("_Opacity"))
                mat.SetFloat("_Opacity", Mathf.Clamp01(Mathf.Max(intensity * 0.25f, 0.08f)));
            if (mat.HasProperty("_Fisheye"))
                mat.SetFloat("_Fisheye", fisheye);
            if (mat.HasProperty("_Scroll"))
                mat.SetFloat("_Scroll", Time.unscaledTime);
            if (mat.HasProperty("_Noise"))
                mat.SetFloat("_Noise", 0.14f);
            if (mat.HasProperty("_Vignette"))
                mat.SetFloat("_Vignette", 0.32f);
            if (mat.HasProperty(MainTexId))
                mat.SetTexture(MainTexId, source);

            Graphics.Blit(source, destination, mat);
            return true;
        }

        private static Material? EnsureMaterial(
            string shaderFindName,
            string materialName,
            ref Material? material,
            ref bool initFailed)
        {
            if (material != null)
                return material;
            if (initFailed)
                return null;

            if (!FpvShaderBundle.TryGetFxShader(shaderFindName, out Shader? shader) || shader == null)
            {
                initFailed = true;
                return null;
            }

            if (!shader.isSupported
                || string.IsNullOrEmpty(shader.name)
                || shader.name.IndexOf("Error", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                initFailed = true;
                return null;
            }

            try
            {
                material = new Material(shader)
                {
                    name = materialName,
                    hideFlags = HideFlags.HideAndDontSave
                };
                return material;
            }
            catch
            {
                initFailed = true;
                return null;
            }
        }
    }
}
