using UnityEngine;

namespace FPVMod.FpvView
{
    internal static class FpvPostProcess
    {
        private static FpvPostProcessBehaviour? _behaviour;

        internal static void Enable(Camera cam)
        {
            if (cam == null)
                return;
            _behaviour = cam.GetComponent<FpvPostProcessBehaviour>();
            if (_behaviour == null)
                _behaviour = cam.gameObject.AddComponent<FpvPostProcessBehaviour>();
            _behaviour.enabled = true;
        }

        internal static void Disable()
        {
            if (_behaviour != null)
                _behaviour.enabled = false;
        }

        internal static void SetNoise(float intensity)
        {
            if (_behaviour != null)
                _behaviour.NoiseIntensity = intensity;
        }
    }

    internal sealed class FpvPostProcessBehaviour : MonoBehaviour
    {
        internal float NoiseIntensity { get; set; }

        private void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if (NoiseIntensity <= 0.01f)
            {
                Graphics.Blit(src, dest);
                return;
            }

            Graphics.Blit(src, dest);
            // Scanline/noise handled by OSD overlay for compatibility without custom shader asset.
        }
    }
}
