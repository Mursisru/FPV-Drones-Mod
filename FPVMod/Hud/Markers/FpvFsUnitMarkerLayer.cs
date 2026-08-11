using System.Collections.Generic;
using FPVMod.Session;
using UnityEngine;

namespace FPVMod.Hud
{
    /// <summary>
    /// Owned FS unit markers: seed/sync like CombatHUD.SetAircraft (factionUnits + trackingDatabase),
    /// visibility round-robin like CombatHUD.UpdateMarkers, project via FPV feed cam.
    /// </summary>
    internal sealed class FpvFsUnitMarkerLayer
    {
        private const float SyncIntervalSec = 0.5f;

        private readonly RectTransform _root;
        private readonly List<FpvFsUnitMarker> _markers = new List<FpvFsUnitMarker>(64);
        private readonly Dictionary<Unit, FpvFsUnitMarker> _lookup = new Dictionary<Unit, FpvFsUnitMarker>(64);
        private readonly HashSet<Unit> _scratchSeen = new HashSet<Unit>();
        private readonly List<Unit> _scratchRemove = new List<Unit>(16);

        private Unit? _ownship;
        private Unit? _drone;
        private float _nextSync;
        private int _visIndex;
        private float _panelW = 1920f;
        private float _panelH = 1080f;

        private FpvFsUnitMarkerLayer(RectTransform root)
        {
            _root = root;
        }

        internal static FpvFsUnitMarkerLayer? Create(RectTransform parent)
        {
            if (parent == null)
                return null;

            Sprite? sprite = FpvUnitIconSprite.Get();
            if (sprite == null)
            {
                FpvPlugin.ModLogger?.LogWarning("FPV FS markers: icon sprite unavailable");
                return null;
            }

            var go = new GameObject("FpvUnitMarkers", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform root = go.GetComponent<RectTransform>();
            GunshipChrome.Stretch(root);
            // Above gunship chrome, below look-around HUD if any.
            root.SetAsLastSibling();
            return new FpvFsUnitMarkerLayer(root);
        }

        internal void SetVisible(bool visible)
        {
            if (_root != null && _root.gameObject.activeSelf != visible)
                _root.gameObject.SetActive(visible);
        }

        internal void Bind(Unit? ownship, Unit? drone)
        {
            _ownship = ownship;
            _drone = drone;
            ClearMarkers();
            _nextSync = 0f;
            SyncFromHq();
        }

        internal void Shutdown()
        {
            ClearMarkers();
            _ownship = null;
            _drone = null;
            if (_root != null)
                Object.Destroy(_root.gameObject);
        }

        internal void Update(Camera? feed, FpvPanelMetrics panel)
        {
            if (_root == null || !_root.gameObject.activeInHierarchy || feed == null)
                return;

            _panelW = panel.Width;
            _panelH = panel.Height;

            if (Time.unscaledTime >= _nextSync)
            {
                _nextSync = Time.unscaledTime + SyncIntervalSec;
                SyncFromHq();
            }

            FactionHQ? hq = ResolveHq();
            if (hq == null || _markers.Count == 0)
                return;

            GlobalPosition viewPos = feed.transform.GlobalPosition();
            Vector3 forward = feed.transform.forward;
            RefreshSelectedFlags();

            // All markers: project each frame (vanilla UpdatePosition).
            for (int i = 0; i < _markers.Count; i++)
            {
                FpvFsUnitMarker m = _markers[i];
                if (!m.Alive)
                    continue;
                m.TickPosition(hq, feed, viewPos, forward, _panelW, _panelH);
                m.ApplySelectedScale();
            }

            // Round-robin visibility (vanilla iconIndex).
            if (_visIndex >= _markers.Count)
                _visIndex = 0;
            if (_markers.Count > 0)
            {
                FpvFsUnitMarker vis = _markers[_visIndex];
                if (vis.Alive)
                    vis.TickVisibility(hq, viewPos);
                _visIndex++;
            }
        }

        private void SyncFromHq()
        {
            FactionHQ? hq = ResolveHq();
            if (hq == null)
                return;

            Sprite? sprite = FpvUnitIconSprite.Get();
            if (sprite == null)
                return;

            _scratchSeen.Clear();

            // Friendly roster (SyncList)
            try
            {
                var friendlies = hq.factionUnits;
                if (friendlies != null)
                {
                    for (int i = 0; i < friendlies.Count; i++)
                        TryAdd(friendlies[i], sprite);
                }
            }
            catch { /* ignore */ }

            // Tracked contacts
            try
            {
                var db = hq.trackingDatabase;
                if (db != null)
                {
                    foreach (KeyValuePair<PersistentID, TrackingInfo> pair in db)
                        TryAdd(pair.Key, sprite);
                }
            }
            catch { /* ignore */ }

            // Prune dead / no longer known to HQ
            _scratchRemove.Clear();
            foreach (KeyValuePair<Unit, FpvFsUnitMarker> kv in _lookup)
            {
                Unit key = kv.Key;
                if (key == null || key.disabled || !_scratchSeen.Contains(key))
                    _scratchRemove.Add(key!);
            }

            for (int i = 0; i < _scratchRemove.Count; i++)
                Remove(_scratchRemove[i]);
        }

        private void TryAdd(PersistentID id, Sprite sprite)
        {
            Unit unit;
            if (!UnitRegistry.TryGetUnit(new PersistentID?(id), out unit))
                return;
            if (unit == null || unit.disabled)
                return;
            if (unit is Scenery)
                return;
            if (_ownship != null && unit == _ownship)
                return;
            if (_drone != null && unit == _drone)
                return;

            _scratchSeen.Add(unit);
            if (_lookup.ContainsKey(unit))
                return;

            FpvFsUnitMarker? marker = FpvFsUnitMarker.Create(_root, unit, sprite);
            if (marker == null)
                return;

            marker.AssessThreat(_ownship);
            _markers.Add(marker);
            _lookup.Add(unit, marker);
        }

        private void Remove(Unit unit)
        {
            if (unit == null)
                return;
            if (!_lookup.TryGetValue(unit, out FpvFsUnitMarker? marker))
                return;
            _lookup.Remove(unit);
            _markers.Remove(marker);
            marker.Destroy();
        }

        private void ClearMarkers()
        {
            for (int i = 0; i < _markers.Count; i++)
                _markers[i].Destroy();
            _markers.Clear();
            _lookup.Clear();
            _visIndex = 0;
        }

        private void RefreshSelectedFlags()
        {
            List<Unit>? targets = null;
            try
            {
                CombatHUD? hud = SceneSingleton<CombatHUD>.i;
                if (hud != null)
                    targets = hud.GetTargetList();
            }
            catch { /* ignore */ }

            for (int i = 0; i < _markers.Count; i++)
            {
                FpvFsUnitMarker m = _markers[i];
                if (!m.Alive)
                    continue;
                bool sel = targets != null && targets.Contains(m.Unit);
                m.SetSelected(sel);
            }
        }

        private static FactionHQ? ResolveHq()
        {
            try
            {
                DynamicMap? map = SceneSingleton<DynamicMap>.i;
                if (map != null && map.HQ != null)
                    return map.HQ;
            }
            catch { /* ignore */ }

            try
            {
                if (FpvControlSession.HeldAircraft != null)
                    return FpvControlSession.HeldAircraft.NetworkHQ;
            }
            catch { /* ignore */ }

            try
            {
                if (FpvControlSession.Drone != null)
                    return FpvControlSession.Drone.NetworkHQ;
            }
            catch { /* ignore */ }

            return null;
        }
    }
}
