using FPVMod.Access;
using FPVMod.Input;
using FPVMod.Session;
using UnityEngine;

namespace FPVMod.Drone
{
    /// <summary>
    /// 50 kg AUW. High static TWR + thrust lapse + always-on drag → ~270 km/h soft ceiling.
    /// COL 0: gravity + drag (energy bleeds, falls) — never vacuum ballistic.
    /// </summary>
    internal sealed class FpvAcroController : MonoBehaviour
    {
        private const float SpanM = FpvConstants.DroneSpanM;
        private const float MassKg = FpvConstants.DroneMassKg;
        private const float MaxTwr = FpvConstants.DroneMaxTwr;
        private const float Cd = FpvConstants.DroneCd;
        private const float StickDz = 0.03f;
        private const float StickExpo = 0.45f;
        private const float G = 9.81f;
        private const float ThrustCut = 0.025f;
        /// <summary>Extra Cd when motors off (windmilling props / blunt coast).</summary>
        private const float IdleCdMul = 1.35f;

        private const float PitchRate = 5.5f;
        private const float RollRate = 5.5f;   // blue/Z — same authority as pitch
        private const float YawRate = 2.5f;    // green/Y — baseline, do not soften
        private const float RateKp = 11f;
        private const float RateKd = 0.55f;
        private const float ZInertiaMul = 2.2f; // mild Izz only

        private Missile? _missile;
        private Rigidbody? _rb;
        private FpvThrustNodes? _nodes;
        private FpvWarhead? _warhead;
        private float _battery;
        private float _launchImpulseLeft;
        private float _maxThrustN;
        private float _refAreaM2;
        private Vector3 _prevRateErr;

        internal float Battery01 => Mathf.Clamp01(_battery / FpvConstants.BatterySeconds);
        internal float SpeedKmh => _rb != null ? _rb.velocity.magnitude * 3.6f : 0f;
        internal float VerticalSpeedMs => _rb != null ? _rb.velocity.y : 0f;
        internal float Collective01 { get; private set; }

        private void Awake()
        {
            _missile = GetComponent<Missile>();
            _rb = GetComponent<Rigidbody>();
            _warhead = GetComponent<FpvWarhead>();
            _battery = FpvConstants.BatterySeconds;
            _refAreaM2 = Mathf.PI * (SpanM * 0.5f) * (SpanM * 0.5f) * 0.48f;
            BindRb();
            FpvMotorKill.KillAll(_missile);
            _nodes = gameObject.GetComponent<FpvThrustNodes>() ?? gameObject.AddComponent<FpvThrustNodes>();
            if (_rb != null)
                _nodes.Ensure(_rb);
        }

        private void BindRb()
        {
            if (_rb == null)
                return;

            _rb.mass = Mathf.Max(1f, MassKg);
            _rb.useGravity = false;
            _rb.isKinematic = false;
            _rb.drag = 0f;
            _rb.angularDrag = 0.35f;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            float warheadFrac = FpvConstants.WarheadMassKg / MassKg;
            _rb.centerOfMass = new Vector3(0f, -0.04f, 0.22f * warheadFrac);

            float m = _rb.mass;
            float arm = SpanM * 0.35f;
            float iPitch = m * arm * arm * 0.7f;
            float iYaw = m * arm * arm * 1.0f;
            float iRoll = m * arm * arm * 0.7f * ZInertiaMul;
            _rb.inertiaTensor = new Vector3(
                Mathf.Max(0.35f, iPitch),
                Mathf.Max(0.4f, iYaw),
                Mathf.Max(0.55f, iRoll));
            _rb.inertiaTensorRotation = Quaternion.identity;

            _maxThrustN = m * G * MaxTwr;
        }

        internal void BoostLaunch(float _)
        {
            FpvMotorKill.KillAll(_missile);
            BindRb();
            _launchImpulseLeft = 0.15f;
            if (_rb == null)
                return;
            _rb.velocity = transform.up * 4f + transform.forward * 1.5f;
            _rb.AddForce(transform.up * (_rb.mass * 2.5f), ForceMode.Impulse);
        }

        internal void ApplyFlight(Missile missile)
        {
            if (_missile == null || _rb == null || missile.disabled)
                return;

            bool controlling = FpvControlSession.IsControlling(missile);
            // Pure clients: only the possessed drone; host LocalSim owns all FPV missiles.
            if (!controlling && !missile.LocalSim)
                return;

            FpvMotorKill.KillAll(missile);
            BindRb();

            _rb.AddForce(Vector3.down * G, ForceMode.Acceleration);

            bool armed = _warhead == null || !_warhead.IsSafe;
            float spd = _rb.velocity.magnitude;
            float cdNow = Cd;

            // Unpossessed / pause: motors off + still resolve ground/unit boom.
            if (!controlling || FpvUiGate.BlocksFlightInput)
            {
                Collective01 = 0f;
                FpvVanillaAero.ApplyDrag(_rb, transform, Cd * IdleCdMul, _refAreaM2);
                FpvImpactResolver.Resolve(missile, _rb, armed);
                return;
            }

            if (_nodes == null)
            {
                _nodes = gameObject.AddComponent<FpvThrustNodes>();
                _nodes.Ensure(_rb);
            }

            try
            {
                FpvReflection.SetField(missile, "airDensity", FpvVanillaAero.SampleAirDensity(_rb.position));
            }
            catch
            {
                // ignore
            }

            FpvInputBridge.Poll(GameManager.playerInput);
            FpvInputSample input = FpvInputBridge.Sample;

            _battery -= Time.fixedDeltaTime;
            float batt = _battery > 0f ? 1f : 0f;

            float pitchIn = ShapeStick(input.Pitch, StickExpo);
            float rollIn = ShapeStick(input.Roll, StickExpo);
            float yawIn = ShapeStick(input.Yaw, StickExpo);

            float collective = Mathf.Clamp01(input.Throttle) * batt;
            if (_launchImpulseLeft > 0f)
            {
                _launchImpulseLeft -= Time.fixedDeltaTime;
                if (collective > ThrustCut)
                {
                    float hover = 1f / (MaxTwr * 0.7071f);
                    collective = Mathf.Max(collective, hover + 0.02f);
                }
            }

            if (collective < ThrustCut)
                collective = 0f;
            Collective01 = collective;

            ApplyRateTorque(pitchIn, rollIn, yawIn);

            if (collective <= 0f)
                cdNow = Cd * IdleCdMul;
            FpvVanillaAero.ApplyDrag(_rb, transform, cdNow, _refAreaM2);

            if (collective > 0f)
            {
                float lapse = FpvThrustNodes.AirspeedLapse(spd);
                _nodes.ApplyThrustNewtons(collective, _maxThrustN, lapse);
            }

            FpvMissileAccess.SetInputs(missile, new Vector3(pitchIn, rollIn, yawIn));
            FpvMissileAccess.SetThrottle(missile, collective);
            FpvMissileAccess.CallUpdateRadarAlt(missile);

            FpvImpactResolver.Resolve(missile, _rb, armed);
        }

        private void ApplyRateTorque(float pitchIn, float rollIn, float yawIn)
        {
            if (_rb == null)
                return;

            Vector3 wantLocal = new Vector3(
                pitchIn * PitchRate,
                yawIn * YawRate,
                -rollIn * RollRate);

            Vector3 haveLocal = transform.InverseTransformDirection(_rb.angularVelocity);
            Vector3 err = wantLocal - haveLocal;
            Vector3 derr = (err - _prevRateErr) / Mathf.Max(Time.fixedDeltaTime, 1e-4f);
            _prevRateErr = err;

            Vector3 accel = err * RateKp - derr * RateKd;
            _rb.AddRelativeTorque(accel, ForceMode.Acceleration);
        }

        private static float ShapeStick(float v, float expo)
        {
            if (Mathf.Abs(v) < StickDz)
                return 0f;
            float s = Mathf.Sign(v);
            float a = Mathf.Clamp01((Mathf.Abs(v) - StickDz) / (1f - StickDz));
            return s * Mathf.Lerp(a, a * a * a, expo);
        }
    }
}
