using FPVMod.Launcher;
using UnityEngine;

namespace FPVMod.Link
{
    internal enum FpvLinkLevel
    {
        Full,
        Degraded,
        Lost
    }

    internal static class FpvLinkQuality
    {
        private const float EvalInterval = 0.2f;
        private const float LostTimeoutSec = 5f;
        private const float LosRadius = 40f;

        private static float _nextEval;
        private static FpvLinkLevel _level = FpvLinkLevel.Full;
        private static float _lostSince;
        private static float _jamSeconds;

        internal static float InputBlend => _level switch
        {
            FpvLinkLevel.Full => 1f,
            FpvLinkLevel.Degraded => 0.55f,
            _ => 0f
        };

        internal static bool LostTimeoutElapsed =>
            _level == FpvLinkLevel.Lost && Time.unscaledTime - _lostSince > LostTimeoutSec;

        internal static void Reset()
        {
            _level = FpvLinkLevel.Full;
            _lostSince = 0f;
            _jamSeconds = 0f;
            _nextEval = 0f;
        }

        internal static FpvLinkLevel Evaluate(Missile drone, FpvLauncher? launcher)
        {
            if (drone == null || drone.disabled)
            {
                _level = FpvLinkLevel.Lost;
                return _level;
            }

            if (Time.unscaledTime < _nextEval)
                return _level;
            _nextEval = Time.unscaledTime + EvalInterval;

            Unit? anchor = launcher != null ? launcher.OwnerUnit : null;
            if (anchor == null)
            {
                _level = FpvLinkLevel.Full;
                return _level;
            }

            float dist = FastMath.Distance(drone.GlobalPosition(), anchor.GlobalPosition());
            if (dist > FpvConstants.LinkRangeM)
            {
                SetLost();
                return _level;
            }

            bool los = HasLineOfSight(drone, anchor);
            bool jammed = IsJammed(drone, dist);

            if (jammed)
            {
                _jamSeconds += EvalInterval;
                _level = _jamSeconds > 2f ? FpvLinkLevel.Lost : FpvLinkLevel.Degraded;
            }
            else if (!los)
            {
                _jamSeconds = 0f;
                _level = FpvLinkLevel.Degraded;
            }
            else
            {
                _jamSeconds = 0f;
                _level = FpvLinkLevel.Full;
            }

            if (_level == FpvLinkLevel.Lost && _lostSince <= 0f)
                _lostSince = Time.unscaledTime;
            else if (_level != FpvLinkLevel.Lost)
                _lostSince = 0f;

            return _level;
        }

        private static void SetLost()
        {
            _level = FpvLinkLevel.Lost;
            if (_lostSince <= 0f)
                _lostSince = Time.unscaledTime;
        }

        private static bool HasLineOfSight(Missile drone, Unit anchor)
        {
            Vector3 a = drone.transform.position + Vector3.up * 0.5f;
            Vector3 b = anchor.transform.position + Vector3.up * 2f;
            if (!Physics.Linecast(a, b, out RaycastHit hit, PhysicsLayers.StaticsMask | PhysicsLayers.ShipsMask))
                return true;
            return hit.distance > Vector3.Distance(a, b) - LosRadius;
        }

        private static bool IsJammed(Missile drone, float dist)
        {
            foreach (JammingPod pod in Object.FindObjectsOfType<JammingPod>())
            {
                if (pod == null || pod.attachedUnit == null || pod.attachedUnit.disabled)
                    continue;
                if (pod.attachedUnit.NetworkHQ != null && drone.NetworkHQ != null &&
                    pod.attachedUnit.NetworkHQ.faction == drone.NetworkHQ.faction)
                    continue;
                float jamRange = 3000f;
                if (FastMath.Distance(drone.GlobalPosition(), pod.attachedUnit.GlobalPosition()) < jamRange)
                    return true;
            }
            return false;
        }
    }
}
