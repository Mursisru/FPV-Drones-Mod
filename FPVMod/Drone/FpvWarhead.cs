using FPVMod.Launcher;
using System.Reflection;
using UnityEngine;

namespace FPVMod.Drone
{
    internal sealed class FpvWarhead : MonoBehaviour
    {
        private static readonly FieldInfo? WarheadField =
            typeof(Missile).GetField("warhead", BindingFlags.Instance | BindingFlags.NonPublic);

        private Missile? _missile;
        private float _spawnTime;
        private bool _armed;

        internal bool IsSafe => !_armed;
        internal float SafeRemaining => Mathf.Max(0f, FpvConstants.ArmingDelaySec - (Time.time - _spawnTime));

        private void Awake()
        {
            _missile = GetComponent<Missile>();
            _spawnTime = Time.time;
            _armed = false;
            SetWarheadArmed(false);
        }

        private void SetWarheadArmed(bool armed)
        {
            if (_missile == null || WarheadField == null)
                return;
            object? wh = WarheadField.GetValue(_missile);
            if (wh == null)
                return;
            typeof(Missile.Warhead).GetField("Armed")?.SetValue(wh, armed);
        }

        private void Update()
        {
            if (_armed || _missile == null)
                return;
            if (Time.time - _spawnTime < FpvConstants.ArmingDelaySec)
                return;
            _armed = true;
            SetWarheadArmed(true);
        }

        internal bool ShouldBlockFriendlyCollision(Unit? other)
        {
            if (_armed || other == null)
                return false;
            FpvLauncher? launcher = other.GetComponentInParent<FpvLauncher>();
            return launcher != null;
        }
    }
}
