using System.Reflection;
using FPVMod.Access;
using FPVMod.Launcher;
using UnityEngine;

namespace FPVMod.Drone
{
    /// <summary>
    /// SAFE window then Arm() + impactFuse. impactFuseDelay must stay 0 (instant boom, not pierce delay).
    /// </summary>
    internal sealed class FpvWarhead : MonoBehaviour
    {
        private static readonly FieldInfo? ImpactFuseField =
            typeof(Missile).GetField("impactFuse", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? ImpactFuseDelayField =
            typeof(Missile).GetField("impactFuseDelay", BindingFlags.Instance | BindingFlags.NonPublic);

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
            SetImpactFuse(false);
            // NOT arming delay — game uses this as post-penetration fuse. 0 = detonate on impact.
            if (ImpactFuseDelayField != null && _missile != null)
                ImpactFuseDelayField.SetValue(_missile, 0f);
            FpvMissileAccess.DisableProxyFuse(_missile!);
        }

        private void FixedUpdate()
        {
            if (_missile == null || _missile.disabled)
                return;

            FpvMissileAccess.DisableProxyFuse(_missile);
            if (ImpactFuseDelayField != null)
                ImpactFuseDelayField.SetValue(_missile, 0f);

            if (_armed)
            {
                SetImpactFuse(true);
                return;
            }

            SetImpactFuse(false);

            if (Time.time - _spawnTime < FpvConstants.ArmingDelaySec)
                return;
            if (!IsClearOfLauncher())
                return;

            _armed = true;
            try
            {
                _missile.Arm();
                _missile.SetTangible(true);
            }
            catch
            {
                // ignore
            }
            SetImpactFuse(true);

            // Instant boom if already inside/near a unit the frame we arm.
            Rigidbody? rb = _missile.rb != null ? _missile.rb : GetComponent<Rigidbody>();
            if (rb != null)
                FpvImpactResolver.Resolve(_missile, rb, true);
        }

        private bool IsClearOfLauncher()
        {
            FpvDroneTag? tag = GetComponent<FpvDroneTag>();
            Unit? owner = tag?.SourceLauncher != null ? tag.SourceLauncher.OwnerUnit : _missile?.owner;
            if (owner == null || owner.disabled)
                return Time.time - _spawnTime > FpvConstants.ArmingDelaySec + 0.5f;

            float dist = Vector3.Distance(transform.position, owner.transform.position);
            return dist > 12f;
        }

        private void SetImpactFuse(bool on)
        {
            if (_missile == null || ImpactFuseField == null)
                return;
            ImpactFuseField.SetValue(_missile, on);
        }

        internal bool ShouldBlockFriendlyCollision(Unit? other)
        {
            if (_armed || other == null)
                return false;
            return other.GetComponentInParent<FpvLauncher>() != null;
        }
    }
}
