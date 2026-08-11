using System.Collections.Generic;
using UnityEngine;

namespace FPVMod.Session
{
    internal static class FpvJetAutopilotHold
    {
        private static Aircraft? _aircraft;
        private static AutopilotPlane? _autopilot;
        private static float _holdAlt;
        private static float _orbitAngle;
        private static GlobalPosition _orbitCenter;
        private static bool _active;

        private static TerrainWarningSystem? _terrainWarning;

        internal static void Enable(Aircraft aircraft)
        {
            if (aircraft == null)
                return;
            _aircraft = aircraft;
            _autopilot = aircraft.GetComponent<AutopilotPlane>();
            _terrainWarning = new TerrainWarningSystem(aircraft);
            _holdAlt = aircraft.transform.position.GlobalY();
            _orbitCenter = aircraft.GlobalPosition();
            _orbitAngle = 0f;
            _active = _autopilot != null;
            BiasOrbitToFriendlyBase();
        }

        internal static void Disable()
        {
            _active = false;
            _aircraft = null;
            _autopilot = null;
            _terrainWarning = null;
        }

        internal static void FixedTick()
        {
            if (!_active || _aircraft == null || _autopilot == null || _aircraft.disabled)
                return;

            _orbitAngle += Time.fixedDeltaTime * 0.05f;
            Vector3 offset = new Vector3(
                Mathf.Cos(_orbitAngle) * FpvConstants.OrbitRadiusM,
                0f,
                Mathf.Sin(_orbitAngle) * FpvConstants.OrbitRadiusM);
            GlobalPosition dest = _orbitCenter + offset;

            float terrainUrgency = _terrainWarning?.urgency ?? 0f;
            float altHold = _holdAlt + terrainUrgency * 200f;
            _autopilot.AutoAim(dest, true, false, false, 0.6f, 45f, true, altHold, Vector3.zero);
        }

        private static void BiasOrbitToFriendlyBase()
        {
            Faction? faction = null;
            if (!GameManager.GetLocalFaction(out faction) || faction == null)
                return;

            Airbase? nearest = null;
            float best = float.MaxValue;
            foreach (Airbase ab in Object.FindObjectsOfType<Airbase>())
            {
                if (ab == null || ab.CurrentHQ == null)
                    continue;
                if (ab.CurrentHQ.faction != faction)
                    continue;
                GlobalPosition abPos = new GlobalPosition(ab.transform.position);
                float d = FastMath.SquareDistance(abPos, _orbitCenter);
                if (d < best)
                {
                    best = d;
                    nearest = ab;
                }
            }

            if (nearest != null)
                _orbitCenter = new GlobalPosition(nearest.transform.position);
        }
    }
}
