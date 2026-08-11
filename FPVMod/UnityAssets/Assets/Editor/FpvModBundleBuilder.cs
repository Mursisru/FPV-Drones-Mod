#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Batch-builds fpvmod_assets into FPVMod/Resources for DLL embedding.</summary>
public static class FpvModBundleBuilder
{
    const string BundleFileName = "fpvmod_assets";
    const string ModelsDir = "Assets/Models";
    const string DroneFbxPath = ModelsDir + "/RPGBoD002fbx.fbx";
    const string DronePrefabPath = ModelsDir + "/fpv_drone_model.prefab";

    [MenuItem("FPV Mod/Build Asset Bundle")]
    public static void BuildAll()
    {
        if (!EnsureDronePrefab())
        {
            Debug.LogError("[FPVMod] Drone FBX missing — copy RPGBoD002fbx.fbx to Assets/Models/");
            EditorApplication.Exit(1);
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string outputDir = Path.GetFullPath(Path.Combine(projectRoot, "..", "Resources"));
        Directory.CreateDirectory(outputDir);

        string[] bundleAssets = AssetDatabase.GetDependencies(DronePrefabPath, true)
            .Where(p => p.StartsWith(ModelsDir + "/") || p.StartsWith(ModelsDir + "\\"))
            .Distinct()
            .ToArray();

        var build = new AssetBundleBuild
        {
            assetBundleName = BundleFileName,
            assetNames = bundleAssets
        };

        BuildPipeline.BuildAssetBundles(
            outputDir,
            new[] { build },
            BuildAssetBundleOptions.ForceRebuildAssetBundle,
            BuildTarget.StandaloneWindows64);

        string bundlePath = Path.Combine(outputDir, BundleFileName);
        if (!File.Exists(bundlePath))
        {
            Debug.LogError("[FPVMod] Bundle build failed — output file not found.");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"[FPVMod] Bundle written: {bundlePath} ({bundleAssets.Length} assets, {new FileInfo(bundlePath).Length} bytes)");
        EditorApplication.Exit(0);
    }

    static bool EnsureDronePrefab()
    {
        ConfigureFbxImporter();

        GameObject? fbxRoot = AssetDatabase.LoadAssetAtPath<GameObject>(DroneFbxPath);
        if (fbxRoot == null)
            return false;

        GameObject inst = Object.Instantiate(fbxRoot);
        inst.name = "fpv_drone_model";
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;
        AssignDroneAlbedo(inst);

        Transform? mount = inst.transform.Find("CameraMount");
        if (mount == null)
        {
            var mountGo = new GameObject("CameraMount");
            mountGo.transform.SetParent(inst.transform, false);
            mountGo.transform.localPosition = new Vector3(0f, 0.08f, 0.18f);
            mountGo.transform.localRotation = Quaternion.Euler(17f, 0f, 0f);
        }

        PrefabUtility.SaveAsPrefabAsset(inst, DronePrefabPath);
        Object.DestroyImmediate(inst);
        AssetDatabase.SaveAssets();
        return true;
    }

    static void AssignDroneAlbedo(GameObject root)
    {
        Texture2D? tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ModelsDir + "/Texture_RPGB.png");
        if (tex == null)
        {
            string[] guids = AssetDatabase.FindAssets("Texture_RPGB t:Texture2D", new[] { ModelsDir });
            if (guids.Length > 0)
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        if (tex == null)
        {
            Debug.LogWarning("[FPVMod] Texture_RPGB.png missing — FBX materials may be untextured.");
            return;
        }

        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null)
                continue;
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null)
                    continue;
                mats[i].mainTexture = tex;
                mats[i].color = Color.white;
            }
            r.sharedMaterials = mats;
        }
    }

    static void ConfigureFbxImporter()
    {
        var importer = AssetImporter.GetAtPath(DroneFbxPath) as ModelImporter;
        if (importer == null)
            return;

        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.bakeAxisConversion = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.SaveAndReimport();
    }
}
#endif
