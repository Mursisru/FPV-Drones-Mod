using NuclearOption.UIStyleSystem;
using UnityEngine;
using UnityEngine.UI;

namespace FPVMod.Launcher
{
    /// <summary>Vanilla airbaseMapIconPrefab clone — orange, half size, pinned to unit map pos.</summary>
    internal sealed class FpvLauncherMapIcon : MapIcon
    {
        internal FpvLauncher? Launcher { get; private set; }

        private Unit? _unit;
        /// <summary>Vanilla airbase uses 50; launcher pick = half.</summary>
        private const float IconScale = 25f;
        /// <summary>Slight lift above unit marker (map Y = world Z).</summary>
        private const float AboveUnitPx = 18f;

        internal void Bind(FpvLauncher launcher, Image? image)
        {
            if (image != null)
                iconImage = image;

            Launcher = launcher;
            _unit = launcher.OwnerUnit;

            // Root stays at layer origin; only iconImage moves (vanilla AirbaseMapIcon pattern).
            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one;
            transform.localEulerAngles = Vector3.zero;

            if (iconImage != null)
            {
                iconImage.sprite = GameAssets.i.airbaseSprite;
                iconImage.raycastTarget = true;
                RectTransform? rt = iconImage.rectTransform;
                if (rt != null)
                {
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                }
            }
            UpdateColor();
        }

        protected override FactionHQ GetHQ() => _unit != null ? _unit.NetworkHQ : null!;

        protected override bool IsLocalPlayerAircraft() => false;

        protected override void OnSelectIcon() { }

        protected override void OnDeselectIcon()
        {
            if (iconImage != null)
                iconImage.raycastTarget = true;
        }

        protected override void OnRemoveIcon() { }

        protected override Color GetColor()
        {
            if (_unit == null || _unit.disabled || Launcher == null || !Launcher.CanLaunch())
                return ThemeManager.Active.ColorTheme.HudUnitNeutral;
            if (isSelected)
                return ThemeManager.Active.ColorTheme.HudUnitSelected;
            return FpvConstants.LauncherMapIconColor;
        }

        public override void ClickIcon(MapIcon.ClickSource clickSource)
        {
            if (Launcher == null || _unit == null || _unit.disabled)
                return;
            if (SceneSingleton<CombatHUD>.i.aircraft != null && !SceneSingleton<CombatHUD>.i.aircraft.disabled)
                return;
            if (GameManager.gameResolution == GameResolution.Defeat)
                return;
            if (!IsFriendly(_unit))
                return;

            DynamicMap map = SceneSingleton<DynamicMap>.i;
            map.UnselectAll();
            map.DeselectAllIcons();
            FpvLauncherMapIcons.DeselectAll();
            SelectIcon();
            FpvLauncherSelectBridge.Select(Launcher, this);
            if (iconImage != null)
                iconImage.raycastTarget = false;
        }

        public override void UpdateIcon(float mapDisplayFactor, float mapInverseScale, Transform mapTransform, bool mapMaximized)
        {
            if (_unit == null || Launcher == null || iconImage == null)
                return;

            UpdateColor();
            bool visible = mapMaximized
                           && SceneSingleton<MapOptions>.i.showAirbaseIcon
                           && IsFriendly(_unit)
                           && !_unit.disabled;
            gameObject.SetActive(visible);
            if (!visible)
                return;

            // Same map-space formula as UnitMapIcon / AirbaseMapIcon (no custom layer math).
            GlobalPosition gp = ResolveUnitMapPosition(_unit);
            globalPosition = gp.AsVector3() * mapDisplayFactor;
            float lift = AboveUnitPx * mapInverseScale;
            iconImage.transform.localPosition = new Vector3(globalPosition.x, globalPosition.z + lift, 0f);
            iconImage.transform.eulerAngles = Vector3.zero;
            iconImage.transform.localScale = mapInverseScale * IconScale * Vector3.one;
        }

        public override string GetInfoText()
        {
            if (_unit == null || Launcher == null)
                return "FPV Launcher";
            if (_unit.disabled)
                return _unit.unitName + "\n(Disabled)";
            if (!Launcher.CanLaunch())
                return _unit.unitName + "\n(Not Ready)";
            return _unit.unitName + "\n(Launch FPV)";
        }

        /// <summary>Match UnitMapIcon: tracking position when available.</summary>
        private static GlobalPosition ResolveUnitMapPosition(Unit unit)
        {
            try
            {
                DynamicMap? map = SceneSingleton<DynamicMap>.i;
                if (map != null && map.TryGetIcon(unit, out UnitMapIcon umi) && umi != null)
                {
                    var tracking = FPVMod.Access.FpvReflection.GetField<object>(umi, "trackingInfo");
                    if (tracking != null)
                    {
                        var mi = tracking.GetType().GetMethod("GetPosition");
                        if (mi?.Invoke(tracking, null) is GlobalPosition tracked)
                            return tracked;
                    }
                }
            }
            catch
            {
                // fall through
            }
            return unit.GlobalPosition();
        }

        private static bool IsFriendly(Unit unit)
        {
            if (!GameManager.GetLocalHQ(out FactionHQ? hq) || hq == null)
                return true;
            return unit.NetworkHQ == hq;
        }
    }
}
