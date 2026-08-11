using FPVMod.Access;
using FPVMod.Drone;
using FPVMod.Launcher;
using UnityEngine;

namespace FPVMod.Bootstrap
{
    /// <summary>Stamps FPV identity onto live Instantate clones of vanilla prefabs (RC pattern).</summary>
    internal static class PrefabFactory
    {
        internal static void StampLauncherInstance(GameObject? go)
        {
            if (go == null)
                return;

            GroundVehicle? vehicle = go.GetComponent<GroundVehicle>();
            if (vehicle == null)
                return;

            VehicleDefinition? def = DefinitionRegistrar.LauncherDefinition;
            if (def != null)
            {
                vehicle.definition = def;
                try { vehicle.NetworkunitName = def.unitName; } catch { /* ignore */ }
            }

            if (go.GetComponent<FpvLauncher>() == null)
                go.AddComponent<FpvLauncher>();

            FpvLauncher launcherComp = go.GetComponent<FpvLauncher>()!;
            FpvPrefabUtil.IsolateMaterials(go);
            FpvPrefabUtil.StripOffensiveSystems(go);
            FpvLauncherAmmoUi.Sync(vehicle, launcherComp);
            FpvLauncherMapIcons.Register(launcherComp);

            go.hideFlags = HideFlags.None;
            if (!go.activeSelf)
                go.SetActive(true);
        }

        internal static void StampDroneInstance(GameObject? go)
        {
            if (go == null)
                return;

            Missile? missile = go.GetComponent<Missile>();
            if (missile == null)
                return;

            MissileDefinition? def = DefinitionRegistrar.DroneDefinition;
            if (def != null)
            {
                missile.definition = def;
                try { missile.NetworkunitName = def.unitName; } catch { /* ignore */ }
            }

            StripSeekers(go);
            Access.FpvReflection.SetField(missile, "seeker", null);

            if (go.GetComponent<FpvDroneTag>() == null) go.AddComponent<FpvDroneTag>();
            if (go.GetComponent<FpvAcroController>() == null) go.AddComponent<FpvAcroController>();
            if (go.GetComponent<FpvWarhead>() == null) go.AddComponent<FpvWarhead>();
            if (go.GetComponent<FpvImpactContact>() == null) go.AddComponent<FpvImpactContact>();

            ApplyDroneVisuals(go);
            EnsureRigidbody(go, FpvConstants.DroneMassKg);
            FpvMotorKill.KillAll(missile);

            go.hideFlags = HideFlags.None;
            if (!go.activeSelf)
                go.SetActive(true);
        }

        internal static void StampByDefinition(GameObject? go, UnitDefinition? def)
        {
            if (go == null || def == null)
                return;
            if (DefinitionRegistrar.IsFpvLauncher(def))
                StampLauncherInstance(go);
            else if (DefinitionRegistrar.IsFpvDrone(def))
                StampDroneInstance(go);
        }

        internal static void StampIfFpvUnit(Unit? unit)
        {
            if (unit == null)
                return;
            if (DefinitionRegistrar.IsFpvLauncher(unit.definition) || unit.GetComponent<FpvLauncher>() != null)
                StampLauncherInstance(unit.gameObject);
            else if (DefinitionRegistrar.IsFpvDrone(unit.definition) || unit.GetComponent<FpvDroneTag>() != null)
                StampDroneInstance(unit.gameObject);
        }

        private static void StripSeekers(GameObject root)
        {
            foreach (MissileSeeker s in root.GetComponentsInChildren<MissileSeeker>(true))
                UnityEngine.Object.DestroyImmediate(s);
        }

        private static void ApplyDroneVisuals(GameObject root)
        {
            Transform? existing = root.transform.Find("FPV_Visual");
            if (existing != null)
            {
                if (!FpvPrefabUtil.HasRenderableMesh(existing.gameObject))
                {
                    FpvPlugin.ModLogger?.LogWarning("StampDrone: stale FPV_Visual without mesh — rebuilding.");
                    UnityEngine.Object.Destroy(existing.gameObject);
                }
                else
                {
                    FinalizeDroneVisual(existing.gameObject, root.transform);
                    return;
                }
            }

            GameObject? model = BundleLoader.LoadAsset<GameObject>("fpv_drone_model");
            if (model == null)
                model = BundleLoader.LoadAsset<GameObject>("rpgbod002fbx");

            if (model == null)
            {
                FpvPlugin.ModLogger?.LogInfo("StampDrone: no bundle model — keeping vanilla mesh.");
                return;
            }

            GameObject vis = UnityEngine.Object.Instantiate(model, root.transform);
            vis.name = "FPV_Visual";
            vis.hideFlags = HideFlags.None;
            vis.SetActive(true);
            FinalizeDroneVisual(vis, root.transform);
        }

        private static void FinalizeDroneVisual(GameObject vis, Transform root)
        {
            vis.hideFlags = HideFlags.None;
            if (!vis.activeSelf)
                vis.SetActive(true);

            FpvPrefabUtil.FitDroneVisualScale(vis, root, FpvConstants.DroneVisualLengthM);
            FpvPrefabUtil.ApplyDroneVisualMaterials(vis, root.gameObject);

            int meshCount = 0;
            foreach (Renderer r in vis.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null)
                    continue;
                r.enabled = true;
                meshCount++;
            }

            if (meshCount == 0 || !FpvPrefabUtil.HasRenderableMesh(vis))
            {
                FpvPlugin.ModLogger?.LogWarning("StampDrone: FBX has no renderers — keeping vanilla mesh.");
                if (vis.name == "FPV_Visual")
                    UnityEngine.Object.Destroy(vis);
                return;
            }

            FpvPrefabUtil.HideRenderersExcept(root.gameObject, vis.transform);

            if (vis.transform.Find("CameraMount") == null)
            {
                var mount = new GameObject("CameraMount");
                mount.transform.SetParent(vis.transform, false);
                mount.transform.localPosition = new Vector3(0f, 0.08f, 0.18f);
                mount.transform.localRotation = Quaternion.Euler(FpvConstants.CameraPitchDeg, 0f, 0f);
            }
        }

        private static void EnsureRigidbody(GameObject root, float mass)
        {
            Rigidbody? rb = root.GetComponent<Rigidbody>();
            if (rb == null)
                rb = root.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.useGravity = true;
            rb.drag = 0f;
            rb.angularDrag = 0.15f;
        }
    }
}
