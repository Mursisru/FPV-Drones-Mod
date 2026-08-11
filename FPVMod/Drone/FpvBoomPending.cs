using System.Collections.Generic;
using UnityEngine;

namespace FPVMod.Drone
{
    /// <summary>
    /// Impact world point for the next FPV Detonate (avoids rb/Datum race).
    /// </summary>
    internal static class FpvBoomPending
    {
        private static readonly Dictionary<int, Vector3> ById = new Dictionary<int, Vector3>(8);

        internal static void Set(Missile missile, Vector3 worldPoint)
        {
            if (missile == null)
                return;
            ById[missile.GetInstanceID()] = worldPoint;
        }

        internal static bool TryConsume(Missile missile, out Vector3 worldPoint)
        {
            worldPoint = default;
            if (missile == null)
                return false;
            int id = missile.GetInstanceID();
            if (!ById.TryGetValue(id, out worldPoint))
                return false;
            ById.Remove(id);
            return true;
        }

        internal static void Clear(Missile missile)
        {
            if (missile == null)
                return;
            ById.Remove(missile.GetInstanceID());
        }
    }
}
