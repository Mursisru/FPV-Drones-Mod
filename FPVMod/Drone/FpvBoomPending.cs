using System.Collections.Generic;
using UnityEngine;

namespace FPVMod.Drone
{
    /// <summary>
    /// Impact world + drone pose at hit time (rb may already be snapped to a bad point before Rpc).
    /// </summary>
    internal static class FpvBoomPending
    {
        private struct Entry
        {
            internal Vector3 World;
            internal Vector3 Drone;
        }

        private static readonly Dictionary<int, Entry> ById = new Dictionary<int, Entry>(8);

        internal static void Set(Missile missile, Vector3 worldPoint, Vector3 dronePos)
        {
            if (missile == null)
                return;
            ById[missile.GetInstanceID()] = new Entry { World = worldPoint, Drone = dronePos };
        }

        internal static void Set(Missile missile, Vector3 worldPoint)
        {
            if (missile == null)
                return;
            Vector3 drone = missile.rb != null ? missile.rb.position : missile.transform.position;
            Set(missile, worldPoint, drone);
        }

        internal static bool TryPeek(Missile missile, out Vector3 worldPoint, out Vector3 droneAtHit)
        {
            worldPoint = default;
            droneAtHit = default;
            if (missile == null)
                return false;
            if (!ById.TryGetValue(missile.GetInstanceID(), out Entry e))
                return false;
            worldPoint = e.World;
            droneAtHit = e.Drone;
            return true;
        }

        internal static bool TryPeek(Missile missile, out Vector3 worldPoint) =>
            TryPeek(missile, out worldPoint, out _);

        internal static bool TryConsume(Missile missile, out Vector3 worldPoint, out Vector3 droneAtHit)
        {
            worldPoint = default;
            droneAtHit = default;
            if (missile == null)
                return false;
            int id = missile.GetInstanceID();
            if (!ById.TryGetValue(id, out Entry e))
                return false;
            ById.Remove(id);
            worldPoint = e.World;
            droneAtHit = e.Drone;
            return true;
        }

        internal static bool TryConsume(Missile missile, out Vector3 worldPoint) =>
            TryConsume(missile, out worldPoint, out _);

        internal static void Clear(Missile missile)
        {
            if (missile == null)
                return;
            ById.Remove(missile.GetInstanceID());
        }
    }
}
