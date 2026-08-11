using System.Collections.Generic;
using FPVMod.Access;
using UnityEngine;
using UnityEngine.UI;

namespace FPVMod.Launcher
{
    /// <summary>Vanilla airbaseMapIconPrefab clones on iconLayer (same space as unit markers).</summary>
    internal static class FpvLauncherMapIcons
    {
        private static readonly Dictionary<FpvLauncher, FpvLauncherMapIcon> Icons = new();
        private static Transform? _layer;

        internal static void Register(FpvLauncher launcher)
        {
            if (launcher == null || Icons.ContainsKey(launcher))
                return;

            DynamicMap? map = SceneSingleton<DynamicMap>.i;
            if (map == null || map.airbaseMapIconPrefab == null)
                return;

            Transform? parent = EnsureLayer(map);
            if (parent == null)
                return;

            GameObject go = Object.Instantiate(map.airbaseMapIconPrefab.gameObject, parent, false);
            go.name = "FpvLauncherMapIcon";
            go.SetActive(false);

            AirbaseMapIcon? stock = go.GetComponent<AirbaseMapIcon>();
            Image? img = stock != null ? stock.iconImage : go.GetComponentInChildren<Image>(true);
            if (stock != null)
                Object.Destroy(stock);

            FpvLauncherMapIcon icon = go.GetComponent<FpvLauncherMapIcon>() ?? go.AddComponent<FpvLauncherMapIcon>();
            icon.Bind(launcher, img);
            Icons[launcher] = icon;
        }

        internal static void SyncAll()
        {
            DynamicMap? map = SceneSingleton<DynamicMap>.i;
            if (map == null)
                return;
            EnsureLayer(map);

            FpvLauncher[] launchers = Object.FindObjectsOfType<FpvLauncher>();
            for (int i = 0; i < launchers.Length; i++)
                Register(launchers[i]);

            List<FpvLauncher>? dead = null;
            foreach (KeyValuePair<FpvLauncher, FpvLauncherMapIcon> kv in Icons)
            {
                if (kv.Key == null || kv.Key.OwnerUnit == null)
                    (dead ??= new List<FpvLauncher>()).Add(kv.Key!);
            }
            if (dead == null)
                return;
            for (int i = 0; i < dead.Count; i++)
                Unregister(dead[i]);
        }

        internal static void Tick(DynamicMap? map = null)
        {
            map ??= SceneSingleton<DynamicMap>.i;
            if (map == null || Icons.Count == 0)
                return;

            float factor = map.mapDisplayFactor;
            float invScale = 1f / map.mapImage.transform.localScale.x;
            Transform mapTransform = map.mapImage.transform;
            bool maximized = DynamicMap.mapMaximized;

            foreach (FpvLauncherMapIcon icon in Icons.Values)
            {
                if (icon != null)
                    icon.UpdateIcon(factor, invScale, mapTransform, maximized);
            }
        }

        internal static void DeselectAll()
        {
            foreach (FpvLauncherMapIcon icon in Icons.Values)
            {
                if (icon != null)
                    icon.DeselectIcon();
            }
        }

        internal static void ClearAll()
        {
            foreach (FpvLauncherMapIcon icon in Icons.Values)
            {
                if (icon != null)
                    icon.RemoveIcon();
            }
            Icons.Clear();
            _layer = null;
        }

        private static void Unregister(FpvLauncher launcher)
        {
            if (!Icons.TryGetValue(launcher, out FpvLauncherMapIcon? icon))
                return;
            icon.RemoveIcon();
            Icons.Remove(launcher);
        }

        private static Transform? EnsureLayer(DynamicMap map)
        {
            if (_layer != null)
                return _layer;

            // Same parent as UnitMapIcon so map-space coords match the blue unit marker.
            if (map.iconLayer != null)
            {
                _layer = map.iconLayer.transform;
                return _layer;
            }

            GameObject? airbaseLayer = FpvReflection.GetField<GameObject>(map, "airbaseLayer");
            _layer = airbaseLayer != null ? airbaseLayer.transform : map.transform;
            return _layer;
        }
    }
}
