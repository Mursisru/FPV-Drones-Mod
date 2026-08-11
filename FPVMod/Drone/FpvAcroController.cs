using FPVMod.Access;
using FPVMod.Input;
using FPVMod.Session;
using UnityEngine;

namespace FPVMod.Drone
{
    internal sealed class FpvAcroController : MonoBehaviour
    {
        private Missile? _missile;
        private Rigidbody? _rb;
        private float _battery;
        private float _thrustRamp;

        internal float Battery01 => Mathf.Clamp01(_battery / FpvConstants.BatterySeconds);
        internal float SpeedKmh => _rb != null ? _rb.velocity.magnitude * 3.6f : 0f;

        private void Awake()
        {
            _missile = GetComponent<Missile>();
            _rb = GetComponent<Rigidbody>();
            _battery = FpvConstants.BatterySeconds;
        }

        internal void ApplyFlight(Missile missile)
        {
            if (_missile == null || _rb == null || missile.disabled)
                return;

            FpvInputSample input = FpvInputBridge.Sample;
            if (!FpvControlSession.IsControlling(missile))
                input = default;

            _battery -= Time.fixedDeltaTime;
            float battMul = _battery > 0f ? 1f : 0f;

            Vector3 stick = new Vector3(input.Pitch, input.Roll, input.Yaw);
            if (stick.sqrMagnitude < 0.04f)
            {
                Vector3 up = transform.up;
                Vector3 levelTorque = Vector3.Cross(up, Vector3.up) * FpvConstants.StabilizerStrength;
                _rb.AddTorque(levelTorque, ForceMode.Acceleration);
            }

            _thrustRamp = Mathf.MoveTowards(_thrustRamp, input.Throttle, Time.fixedDeltaTime * 1.5f);
            FpvMissileAccess.SetInputs(_missile, stick);
            FpvMissileAccess.SetThrottle(_missile, _thrustRamp * battMul);

            float thrust = FpvConstants.AcroThrust * _thrustRamp * battMul;
            _rb.AddForce(transform.forward * thrust, ForceMode.Acceleration);

            if (_rb.velocity.magnitude > FpvConstants.MaxSpeedMs)
                _rb.velocity = _rb.velocity.normalized * FpvConstants.MaxSpeedMs;

            FpvMissileAccess.CallUpdateRadarAlt(_missile);
            FpvMissileAccess.CallApplyAero(_missile);
            FpvMissileAccess.CallDetectCollisions(_missile);
        }
    }
}
