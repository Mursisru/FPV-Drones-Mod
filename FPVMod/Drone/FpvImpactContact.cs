using UnityEngine;

namespace FPVMod.Drone
{
    /// <summary>
    /// PhysX contacts with moving units — raycasts often miss after separation.
    /// </summary>
    internal sealed class FpvImpactContact : MonoBehaviour
    {
        private Missile? _missile;
        private Rigidbody? _rb;
        private FpvWarhead? _warhead;

        private void Awake()
        {
            _missile = GetComponent<Missile>();
            _rb = GetComponent<Rigidbody>();
            _warhead = GetComponent<FpvWarhead>();
        }

        private void OnCollisionEnter(Collision collision) => Handle(collision);

        private void OnCollisionStay(Collision collision) => Handle(collision);

        private void Handle(Collision collision)
        {
            if (_missile == null || _rb == null || _missile.disabled)
                return;
            bool armed = _warhead == null || !_warhead.IsSafe;
            FpvImpactResolver.ResolveContact(_missile, _rb, armed, collision);
        }
    }
}
