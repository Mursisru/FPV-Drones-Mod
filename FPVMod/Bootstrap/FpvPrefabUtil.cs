using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FPVMod.Bootstrap
{
    internal static class FpvPrefabUtil
    {
        private static readonly string[] LauncherTemplateKeys =
        {
            "Truck2-MLRS",
            "Truck2_MLRS1",
            "Truck2_MLRS",
            "MobileMLRS1",
            "MobileMLRS"
        };

        internal static VehicleDefinition? ResolveLauncherTemplate(Encyclopedia enc)
        {
            if (Encyclopedia.Lookup != null)
            {
                foreach (string key in LauncherTemplateKeys)
                {
                    if (Encyclopedia.Lookup.TryGetValue(key, out UnitDefinition def) && def is VehicleDefinition vd && vd.unitPrefab != null)
                        return vd;
                }
            }

            VehicleDefinition? mlrs = enc.vehicles?.FirstOrDefault(v =>
                v?.unitPrefab != null &&
                !string.IsNullOrEmpty(v.jsonKey) &&
                v.jsonKey.IndexOf("MLRS", StringComparison.OrdinalIgnoreCase) >= 0 &&
                v.jsonKey.IndexOf("rocket", StringComparison.OrdinalIgnoreCase) < 0);

            if (mlrs != null)
                return mlrs;

            LogVehicleHints(enc);
            return null;
        }

        internal static MissileDefinition? ResolveDroneTemplate(Encyclopedia enc)
        {
            string[] prefer = { "bomb_125", "AGM", "Cruise", "missile", "bomb" };
            foreach (string key in prefer)
            {
                MissileDefinition? m = enc.missiles?.FirstOrDefault(d =>
                    d?.unitPrefab != null &&
                    !string.IsNullOrEmpty(d.jsonKey) &&
                    d.jsonKey.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);
                if (m != null)
                    return m;
            }

            return enc.missiles?.FirstOrDefault(d => d?.unitPrefab != null);
        }

        internal static void IsolateMaterials(GameObject root)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null)
                    continue;
                Material[] mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null)
                        mats[i] = new Material(mats[i]);
                }
                r.materials = mats;
            }
        }

        internal static void StripOffensiveSystems(GameObject root)
        {
            foreach (MissileLauncher launcher in root.GetComponentsInChildren<MissileLauncher>(true))
            {
                launcher.enabled = false;
                UnityEngine.Object.Destroy(launcher);
            }

            foreach (MountedMissile mount in root.GetComponentsInChildren<MountedMissile>(true))
            {
                mount.enabled = false;
                UnityEngine.Object.Destroy(mount);
            }

            foreach (Weapon w in root.GetComponentsInChildren<Weapon>(true))
            {
                if (w is Gun || w is Laser || w is JammingPod)
                {
                    w.enabled = false;
                    UnityEngine.Object.Destroy(w);
                }
            }
        }

        internal static Material? PickReferenceMaterial(GameObject root)
        {
            Transform? skip = root.transform.Find("FPV_Visual");
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null)
                    continue;
                if (skip != null && (r.transform == skip || r.transform.IsChildOf(skip)))
                    continue;

                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null)
                        return mats[i];
                }
            }

            return null;
        }

        internal static void ApplyReferenceMaterial(GameObject visual, Material reference)
        {
            if (visual == null || reference == null)
                return;

            foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null)
                    continue;
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = reference;
                r.sharedMaterials = mats;
            }
        }

        internal static void HideRenderersExcept(GameObject root, Transform keepSubtree)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null)
                    continue;
                if (keepSubtree != null && (r.transform == keepSubtree || r.transform.IsChildOf(keepSubtree)))
                    continue;
                r.enabled = false;
            }
        }

        /// <summary>Scale + orient visual; undo parent bomb prefab micro-scale.</summary>
        internal static void FitDroneVisualScale(GameObject visual, Transform parent, float targetLengthM)
        {
            if (visual == null || parent == null || targetLengthM <= 0f)
                return;

            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(
                FpvConstants.DroneVisualRotX,
                FpvConstants.DroneVisualRotY,
                FpvConstants.DroneVisualRotZ);
            visual.transform.localScale = Vector3.one;

            float meshExtent = GetMaxMeshLocalExtent(visual);
            if (meshExtent < 0.001f)
            {
                FpvPlugin.ModLogger?.LogWarning("StampDrone: mesh extent ~0 — fallback 1m scale target.");
                meshExtent = 1f;
            }

            Vector3 ps = parent.lossyScale;
            float parentFactor = Mathf.Max(Mathf.Abs(ps.x), Mathf.Abs(ps.y), Mathf.Abs(ps.z));
            if (parentFactor < 0.0001f)
                parentFactor = 1f;

            float localScale = targetLengthM / meshExtent / parentFactor;
            visual.transform.localScale = Vector3.one * localScale;
        }

        /// <summary>Game-compatible materials: vanilla shader + bundle albedo per submesh slot.</summary>
        internal static void ApplyDroneVisualMaterials(GameObject visual, GameObject missileRoot)
        {
            if (visual == null)
                return;

            Material? refMat = PickReferenceMaterial(missileRoot);
            Shader? shader = refMat?.shader;
            if (shader == null)
                shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            if (shader == null)
            {
                FpvPlugin.ModLogger?.LogWarning("StampDrone: no usable shader for FBX materials.");
                return;
            }

            Texture? defaultAlbedo = BundleLoader.LoadDroneAlbedoTexture();

            foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null)
                    continue;

                Material[] shared = r.sharedMaterials;
                if (shared.Length == 0)
                    continue;

                Material[] instanced = new Material[shared.Length];
                for (int i = 0; i < shared.Length; i++)
                {
                    Material? src = shared[i];
                    Material mat = new Material(shader);
                    Texture? albedo = ResolveSlotAlbedo(src, defaultAlbedo);

                    if (albedo != null)
                    {
                        ApplyAlbedo(mat, albedo, src);
                    }
                    else if (src != null)
                    {
                        mat.color = src.color;
                        CopySecondaryMaps(src, mat);
                    }
                    else
                    {
                        mat.color = Color.white;
                    }

                    instanced[i] = mat;
                }

                r.materials = instanced;
            }
        }

        private static Texture? ResolveSlotAlbedo(Material? src, Texture? fallback)
        {
            if (src?.mainTexture != null)
                return src.mainTexture;
            return fallback;
        }

        private static void ApplyAlbedo(Material mat, Texture albedo, Material? src)
        {
            mat.color = Color.white;
            if (mat.HasProperty("_MainTex"))
            {
                mat.mainTexture = albedo;
                if (src != null && mat.HasProperty("_MainTex_ST"))
                    mat.SetTextureScale("_MainTex", src.mainTextureScale);
            }

            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", albedo);

            if (mat.HasProperty("_Glossiness"))
                mat.SetFloat("_Glossiness", 0.35f);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", 0.05f);
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", Color.black);
        }

        private static void CopySecondaryMaps(Material src, Material dst)
        {
            if (src.HasProperty("_BumpMap") && dst.HasProperty("_BumpMap") && src.GetTexture("_BumpMap") != null)
                dst.SetTexture("_BumpMap", src.GetTexture("_BumpMap"));
            if (src.HasProperty("_MetallicGlossMap") && dst.HasProperty("_MetallicGlossMap") &&
                src.GetTexture("_MetallicGlossMap") != null)
                dst.SetTexture("_MetallicGlossMap", src.GetTexture("_MetallicGlossMap"));
        }

        internal static bool HasRenderableMesh(GameObject root)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled)
                    continue;
                if (r is MeshRenderer mr)
                {
                    MeshFilter? mf = mr.GetComponent<MeshFilter>();
                    if (mf?.sharedMesh != null)
                        return true;
                }
                else if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                    return true;
            }

            return false;
        }

        private static float GetMaxMeshLocalExtent(GameObject visualRoot)
        {
            Transform root = visualRoot.transform;
            float max = 0f;

            foreach (MeshFilter mf in visualRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf?.sharedMesh == null)
                    continue;
                Vector3 size = mf.sharedMesh.bounds.size;
                Vector3 chain = AccumulateLocalScale(mf.transform, root);
                max = Mathf.Max(max, size.x * chain.x, size.y * chain.y, size.z * chain.z);
            }

            foreach (SkinnedMeshRenderer smr in visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr?.sharedMesh == null)
                    continue;
                Vector3 size = smr.sharedMesh.bounds.size;
                Vector3 chain = AccumulateLocalScale(smr.transform, root);
                max = Mathf.Max(max, size.x * chain.x, size.y * chain.y, size.z * chain.z);
            }

            return max;
        }

        private static Vector3 AccumulateLocalScale(Transform from, Transform stopBefore)
        {
            Vector3 s = from.localScale;
            Transform? t = from.parent;
            while (t != null && t != stopBefore)
            {
                s = Vector3.Scale(s, t.localScale);
                t = t.parent;
            }

            return new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }

        private static float GetMaxMeshExtent(GameObject root)
        {
            return GetMaxMeshLocalExtent(root);
        }

        private static void LogVehicleHints(Encyclopedia enc)
        {
            if (enc.vehicles == null)
                return;
            var hints = new List<string>();
            foreach (VehicleDefinition v in enc.vehicles)
            {
                if (v == null || string.IsNullOrEmpty(v.jsonKey))
                    continue;
                if (v.jsonKey.IndexOf("MLRS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    v.jsonKey.IndexOf("Truck2", StringComparison.OrdinalIgnoreCase) >= 0)
                    hints.Add(v.jsonKey);
            }
            if (hints.Count > 0)
                FpvPlugin.ModLogger?.LogWarning($"FPV launcher template missing. MLRS candidates: {string.Join(", ", hints)}");
        }
    }
}
