using UnityEngine;

namespace FPVMod.Drone
{
    /// <summary>
    /// Instant kamikaze on first contact/proximity with units after ARM.
    /// Does not wait for slow raycast-only paths or OnCollisionStay.
    /// </summary>
    internal sealed class FpvImpactContact : MonoBehaviour
    {
        private Missile? _missile;
        private Rigidbody? _rb;
        private FpvWarhead? _warhead;
        private bool _wasArmed;

        private void Awake()
        {
            _missile = GetComponent<Missile>();
            _rb = GetComponent<Rigidbody>();
            _warhead = GetComponent<FpvWarhead>();
            if (_rb != null)
            {
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        private void FixedUpdate()
        {
            if (_missile == null || _rb == null || _missile.disabled)
                return;

            bool armed = _warhead == null || !_warhead.IsSafe;
            if (!armed)
            {
                _wasArmed = false;
                return;
            }

            try { _missile.SetTangible(true); } catch { /* ignore */ }

            // Full Resolve: units + terrain (PulseUnits alone skips Statics → long fall, no boom).
            if (!_wasArmed)
                _wasArmed = true;
            FpvImpactResolver.Resolve(_missile, _rb, true);
        }

        private void OnCollisionEnter(Collision collision) => BoomFromCollision(collision);

        private void OnCollisionStay(Collision collision) => BoomFromCollision(collision);

        private void OnTriggerEnter(Collider other) => BoomFromTrigger(other);

        private void OnTriggerStay(Collider other) => BoomFromTrigger(other);

        private void BoomFromCollision(Collision collision)
        {
            if (_missile == null || _rb == null || _missile.disabled)
                return;
            if (_warhead != null && _warhead.IsSafe)
                return;
            if (collision == null || collision.contactCount < 1)
                return;

            Collider? other = collision.GetContact(0).otherCollider ?? collision.collider;
            FpvPlugin.ModLogger?.LogInfo(
                $"FpvImpact: PhysX contact other={other?.name} layer={other?.gameObject.layer} rb={other?.attachedRigidbody != null}");

            FpvImpactResolver.ResolveContact(_missile, _rb, true, collision);
        }

        private void BoomFromTrigger(Collider other)
        {
            if (_missile == null || _rb == null || _missile.disabled || other == null)
                return;
            if (_warhead != null && _warhead.IsSafe)
                return;
            FpvImpactResolver.ResolveTrigger(_missile, _rb, other);
        }
    }
}
