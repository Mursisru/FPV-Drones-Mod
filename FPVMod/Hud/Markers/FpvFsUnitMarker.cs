using NuclearOption.UIStyleSystem;
using UnityEngine;
using UnityEngine.UI;

namespace FPVMod.Hud
{
    /// <summary>Single FS unit marker — CombatHUD/HUDUnitMarker rules, custom sprite.</summary>
    internal sealed class FpvFsUnitMarker
    {
        private const float FreshFlashSec = 1f;
        private const float FreshMaximizeSec = 4f;
        private const float OutdatedAccuracyM = 20f;
        private const float MinScaleEnemy = 6f;
        private const float SelectedScale = 20f;
        private const float DistScaleFactor = 4E-05f;

        private readonly Unit _unit;
        private readonly Image _image;
        private readonly RectTransform _rt;
        private readonly bool _alwaysMaximized;
        private readonly float _baseScale;
        private TrackingInfo? _tracking;
        private float _threat;
        private float _customScale;
        private float _customRange = 15000f;
        private float _distanceScale = 1f;
        private float _timeCreated;
        private bool _fresh = true;
        private bool _maximized = true;
        private bool _hideAsMinimizedFriendly;
        private bool _outdated;
        private bool _selected;
        private Color _factionColor = Color.white;

        internal Unit Unit => _unit;
        internal bool Alive => _unit != null && !_unit.disabled && _image != null;

        private FpvFsUnitMarker(Unit unit, Image image)
        {
            _unit = unit;
            _image = image;
            _rt = image.rectTransform;
            _alwaysMaximized = unit is Aircraft;
            _baseScale = unit.definition != null ? unit.definition.iconSize : 1f;
            _timeCreated = Time.timeSinceLevelLoad;
            _image.enabled = false;
            _image.raycastTarget = false;
            RefreshCustomize();
            AssessThreat();
            RefreshFactionVisual();
        }

        internal static FpvFsUnitMarker? Create(RectTransform parent, Unit unit, Sprite sprite)
        {
            if (parent == null || unit == null || sprite == null)
                return null;

            var go = new GameObject($"FpvMk[{unit.persistentID}]", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            // Vanilla unitMarker Image is ~1×1; size comes from localScale (hmdIconSize ≈ 30).
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;

            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.color = Color.white;
            img.enabled = false;
            img.raycastTarget = false;

            return new FpvFsUnitMarker(unit, img);
        }

        internal void Destroy()
        {
            if (_outdated && _tracking != null)
            {
                try { _tracking.OnSpotted -= OnSpotted; } catch { /* ignore */ }
            }
            if (_image != null)
                UnityEngine.Object.Destroy(_image.gameObject);
        }

        internal void AssessThreat(Unit? assessor = null)
        {
            Unit? src = assessor;
            if (src == null)
            {
                try { src = SceneSingleton<CombatHUD>.i?.aircraft; } catch { /* ignore */ }
            }
            if (src == null || src.definition == null || _unit.definition == null)
            {
                _threat = 0f;
                return;
            }
            _threat = src.definition.ThreatPosedBy(_unit.definition.roleIdentity);
        }

        internal void RefreshCustomize()
        {
            float hmd = 30f;
            try { hmd = PlayerSettings.hmdIconSize; } catch { /* ignore */ }
            _customScale = _baseScale * hmd;

            try
            {
                HUDOptions? opts = SceneSingleton<HUDOptions>.i;
                if (opts != null)
                    _customRange = opts.CheckMaximizeIcon(_unit);
            }
            catch
            {
                _customRange = 15000f;
            }
        }

        internal void RefreshFactionVisual()
        {
            FactionMode mode = FactionMode.NoFaction;
            try { mode = DynamicMap.GetFactionMode(_unit.NetworkHQ, false); }
            catch { /* ignore */ }

            ColorTheme? theme = null;
            try { theme = ThemeManager.Active?.ColorTheme; } catch { /* ignore */ }

            switch (mode)
            {
                case FactionMode.Friendly:
                    _factionColor = theme != null ? theme.HudUnitFriendly : new Color(0.2f, 0.85f, 0.3f, 1f);
                    break;
                case FactionMode.Enemy:
                    _factionColor = theme != null ? theme.HudUnitHostile : new Color(0.95f, 0.2f, 0.15f, 1f);
                    break;
                default:
                    _factionColor = theme != null ? theme.HudUnitNeutral : new Color(0.85f, 0.85f, 0.2f, 1f);
                    break;
            }

            if (_image != null && _image.sprite == null)
            {
                Sprite? s = FpvUnitIconSprite.Get();
                if (s != null)
                    _image.sprite = s;
            }
        }

        internal void SetSelected(bool selected)
        {
            if (_selected == selected)
                return;
            _selected = selected;
            ApplyColor();
        }

        /// <summary>Round-robin visibility / maximize / outdated (CombatHUD.iconIndex style).</summary>
        internal void TickVisibility(FactionHQ hq, GlobalPosition viewPos)
        {
            if (!Alive || hq == null)
                return;

            if (!_outdated && !hq.IsTargetPositionAccurate(_unit, OutdatedAccuracyM))
                SetOutdated(true);

            if (_selected)
                return;

            GlobalPosition known;
            if (!hq.TryGetKnownPosition(_unit, out known))
                return;

            bool enemy = hq != _unit.NetworkHQ;
            UpdateMaximized(known, viewPos, enemy);
        }

        /// <summary>Every-frame screen project via feed camera.</summary>
        internal void TickPosition(
            FactionHQ hq,
            Camera feed,
            GlobalPosition viewPos,
            Vector3 camForward,
            float panelW,
            float panelH)
        {
            if (!Alive || feed == null)
                return;

            GlobalPosition global = _unit.GlobalPosition();
            if (_outdated && (hq == null || !hq.TryGetKnownPosition(_unit, out global)))
            {
                if (_image.enabled)
                    _image.enabled = false;
                return;
            }

            Vector3 world = global.ToLocalPosition();
            if (_hideAsMinimizedFriendly && !_selected)
            {
                if (_image.enabled)
                    _image.enabled = false;
                return;
            }

            if (Vector3.Dot(global - viewPos, camForward) < 0f)
            {
                if (_image.enabled)
                    _image.enabled = false;
                return;
            }

            Vector3 vp = feed.WorldToViewportPoint(world);
            if (vp.z <= 0f || vp.x < -0.05f || vp.x > 1.05f || vp.y < -0.05f || vp.y > 1.05f)
            {
                if (_image.enabled)
                    _image.enabled = false;
                return;
            }

            if (!_image.enabled)
                _image.enabled = true;

            _rt.anchoredPosition = new Vector2((vp.x - 0.5f) * panelW, (vp.y - 0.5f) * panelH);
            TickFreshFlash();
        }

        private void TickFreshFlash()
        {
            if (!_fresh || _selected)
                return;

            float age = Time.timeSinceLevelLoad - _timeCreated;
            Color warn = _factionColor;
            try
            {
                ColorTheme? theme = ThemeManager.Active?.ColorTheme;
                if (theme != null)
                    warn = _factionColor + theme.Warning;
            }
            catch { /* ignore */ }

            _image.color = Color.Lerp(warn, ResolveDrawColor(), Mathf.Clamp01(age));
            if (age > FreshFlashSec)
            {
                _fresh = false;
                ApplyColor();
            }
        }

        private void UpdateMaximized(GlobalPosition known, GlobalPosition viewPos, bool enemy)
        {
            if (_alwaysMaximized)
            {
                _maximized = true;
                _hideAsMinimizedFriendly = false;
                float dist = FastMath.Distance(viewPos, known);
                _distanceScale = Mathf.Lerp(1f, 0.45f, dist * DistScaleFactor - 0.5f);
                _rt.localScale = _customScale * _distanceScale * Vector3.one;
                EnsureSprite();
                return;
            }

            float age = Time.timeSinceLevelLoad - _timeCreated;
            if (age <= FreshMaximizeSec)
                _maximized = true;
            else
                _maximized = FastMath.InRange(known, viewPos, (0.1f + _threat) * _customRange);

            if (_maximized)
            {
                _hideAsMinimizedFriendly = false;
                _rt.localScale = _customScale * _distanceScale * Vector3.one;
                EnsureSprite();
                return;
            }

            // Vanilla: hostile → minimizedHostile @ scale 6; friendly → sprite null (hidden).
            if (enemy)
            {
                _hideAsMinimizedFriendly = false;
                _rt.localScale = MinScaleEnemy * Vector3.one;
                EnsureSprite();
            }
            else
            {
                _hideAsMinimizedFriendly = true;
                if (_image.enabled)
                    _image.enabled = false;
            }
        }

        private void SetOutdated(bool outdated)
        {
            if (_outdated == outdated)
                return;

            if (_tracking == null)
            {
                try
                {
                    DynamicMap? map = SceneSingleton<DynamicMap>.i;
                    if (map != null && map.HQ != null)
                        _tracking = map.HQ.GetTrackingData(_unit.persistentID);
                }
                catch { /* ignore */ }
            }

            _outdated = outdated;
            if (_outdated)
            {
                if (_tracking != null)
                    _tracking.OnSpotted += OnSpotted;
            }
            else if (_tracking != null)
            {
                _tracking.OnSpotted -= OnSpotted;
            }

            ApplyColor();
        }

        private void OnSpotted()
        {
            if (_outdated)
                SetOutdated(false);
        }

        private void EnsureSprite()
        {
            if (_image.sprite != null)
                return;
            Sprite? s = FpvUnitIconSprite.Get();
            if (s != null)
                _image.sprite = s;
        }

        private void ApplyColor() => _image.color = ResolveDrawColor();

        private Color ResolveDrawColor()
        {
            if (_selected)
            {
                try
                {
                    ColorTheme? theme = ThemeManager.Active?.ColorTheme;
                    if (theme != null)
                        return theme.HudUnitSelected;
                }
                catch { /* ignore */ }
                return Color.white;
            }

            float a = (_outdated && _maximized) ? 0.5f : 1f;
            return _factionColor.WithAlpha(a);
        }

        internal void ApplySelectedScale()
        {
            if (_selected)
                _rt.localScale = Vector3.one * SelectedScale;
        }
    }
}
