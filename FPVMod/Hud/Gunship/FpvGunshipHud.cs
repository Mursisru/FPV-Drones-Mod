using FPVMod.Ui;
using UnityEngine;

namespace FPVMod.Hud
{
    /// <summary>Owned COD AC-130 gunship FS HUD (ported from current MC Gunship).</summary>
    internal sealed class FpvGunshipHud
    {
        private readonly RectTransform _root;
        private readonly GunshipTvOverlay _tv;
        private readonly GunshipTelemetry _telemetry;
        private readonly GunshipCrosshair _crosshair;
        private readonly GunshipRangeScale _range;
        private readonly GunshipWeaponStatus _weapons;
        private readonly GunshipNavFooter _nav;
        private float _layoutW = -1f;
        private float _layoutH = -1f;

        private FpvGunshipHud(
            RectTransform root,
            GunshipTvOverlay tv,
            GunshipTelemetry telemetry,
            GunshipCrosshair crosshair,
            GunshipRangeScale range,
            GunshipWeaponStatus weapons,
            GunshipNavFooter nav)
        {
            _root = root;
            _tv = tv;
            _telemetry = telemetry;
            _crosshair = crosshair;
            _range = range;
            _weapons = weapons;
            _nav = nav;
        }

        internal RectTransform Root => _root;

        internal static FpvGunshipHud Create(RectTransform parent)
        {
            var rootGo = new GameObject("FpvGunshipHud", typeof(RectTransform));
            rootGo.transform.SetParent(parent, false);
            RectTransform root = rootGo.GetComponent<RectTransform>();
            GunshipChrome.Stretch(root);

            return new FpvGunshipHud(
                root,
                GunshipTvOverlay.Create(root),
                GunshipTelemetry.Create(root),
                GunshipCrosshair.Create(root),
                GunshipRangeScale.Create(root),
                GunshipWeaponStatus.Create(root),
                GunshipNavFooter.Create(root));
        }

        internal void SetVisible(bool visible)
        {
            if (_root != null && _root.gameObject.activeSelf != visible)
                _root.gameObject.SetActive(visible);
        }

        internal void SetTvOverlayEnabled(bool enabled) => _tv.SetEnabled(enabled);

        internal void Shutdown()
        {
            try { _tv.Shutdown(); } catch { /* ignore */ }
            try { _telemetry.Shutdown(); } catch { /* ignore */ }
            try
            {
                if (_root != null)
                    Object.Destroy(_root.gameObject);
            }
            catch { /* ignore */ }
        }

        internal void Update(FpvGunshipSnapshot snapshot, FpvPanelMetrics panel)
        {
            EnsureLayout(panel);
            _tv.Update();
            _telemetry.Update(snapshot);
            _crosshair.Update(snapshot, panel);
            _range.Update(snapshot);
            _weapons.Update(snapshot);
            _nav.Update(snapshot);
        }

        private void EnsureLayout(FpvPanelMetrics panel)
        {
            if (Mathf.Approximately(panel.Width, _layoutW)
                && Mathf.Approximately(panel.Height, _layoutH))
                return;

            _layoutW = panel.Width;
            _layoutH = panel.Height;
            _telemetry.Place(panel);
            _crosshair.Place(panel);
            _range.Place(panel);
            _weapons.Place(panel);
            _nav.Place(panel);
        }
    }
}
