using FPVMod.Access;
using FPVMod.Drone;
using FPVMod.Session;
using UnityEngine;
using UnityEngine.Audio;

namespace FPVMod.Audio
{
    /// <summary>
    /// FPV motor + airframe audio. Owned session: 2D (listener on feed).
    /// Unpossessed LocalSim: quiet 3D so nearby players can hear.
    /// </summary>
    internal sealed class FpvDroneMotorAudio : MonoBehaviour
    {
        private const float MinVol = 0.02f;
        private const float MaxMotorVol = 0.55f;
        private const float MaxAirVol = 0.28f;
        private const float IdlePitch = 0.72f;
        private const float FullPitch = 1.55f;
        private const float AirPitchBase = 0.85f;
        private const float AirPitchPerMs = 0.012f;
        private const float SmoothHz = 8f;
        private const float PossessedSpatial = 0f;
        private const float RemoteSpatial = 1f;

        private AudioSource? _motor;
        private AudioSource? _air;
        private FpvAcroController? _acro;
        private Missile? _missile;
        private float _motorVol;
        private float _airVol;
        private float _motorPitch = 1f;
        private bool _vanillaMuted;

        private void Awake()
        {
            _missile = GetComponent<Missile>();
            _acro = GetComponent<FpvAcroController>();
            MuteVanillaFlightSound();
            EnsureSources();
        }

        private void OnEnable()
        {
            MuteVanillaFlightSound();
            EnsureSources();
            if (_motor != null && !_motor.isPlaying)
                _motor.Play();
            if (_air != null && !_air.isPlaying)
                _air.Play();
        }

        private void OnDisable()
        {
            if (_motor != null)
                _motor.Stop();
            if (_air != null)
                _air.Stop();
        }

        private void Update()
        {
            if (_missile == null || _missile.disabled)
            {
                FadeOut(Time.deltaTime);
                return;
            }

            MuteVanillaFlightSound();
            EnsureSources();

            bool controlling = FpvControlSession.IsControlling(_missile);
            bool boom = FpvControlSession.BoomSpectating;
            float col = _acro != null ? _acro.Collective01 : 0f;
            float spd = _acro != null ? _acro.SpeedKmh / 3.6f : 0f;

            float wantMotor = 0f;
            float wantAir = 0f;
            float wantPitch = IdlePitch;

            if (!boom && (controlling || _missile.LocalSim))
            {
                float thr = Mathf.Clamp01(col);
                wantMotor = Mathf.Lerp(MinVol, MaxMotorVol, thr * thr * 0.65f + thr * 0.35f);
                wantPitch = Mathf.Lerp(IdlePitch, FullPitch, thr);
                // Spinning props idle whisper when stick low but session active.
                if (controlling && thr < 0.02f)
                    wantMotor = MinVol * 0.6f;

                float air01 = Mathf.Clamp01(spd / 55f);
                wantAir = air01 * air01 * MaxAirVol;
                if (!controlling)
                {
                    wantMotor *= 0.35f;
                    wantAir *= 0.45f;
                }
            }

            float k = 1f - Mathf.Exp(-SmoothHz * Time.deltaTime);
            _motorVol = Mathf.Lerp(_motorVol, wantMotor, k);
            _airVol = Mathf.Lerp(_airVol, wantAir, k);
            _motorPitch = Mathf.Lerp(_motorPitch, wantPitch, k);

            float spatial = controlling || boom ? PossessedSpatial : RemoteSpatial;
            ApplySource(_motor, _motorVol, _motorPitch, spatial);
            float airPitch = AirPitchBase + spd * AirPitchPerMs;
            ApplySource(_air, _airVol, airPitch, spatial);
        }

        private void FadeOut(float dt)
        {
            float k = 1f - Mathf.Exp(-SmoothHz * dt);
            _motorVol = Mathf.Lerp(_motorVol, 0f, k);
            _airVol = Mathf.Lerp(_airVol, 0f, k);
            ApplySource(_motor, _motorVol, _motorPitch, PossessedSpatial);
            ApplySource(_air, _airVol, 1f, PossessedSpatial);
        }

        private static void ApplySource(AudioSource? src, float vol, float pitch, float spatial)
        {
            if (src == null)
                return;
            src.volume = Mathf.Clamp01(vol);
            src.pitch = Mathf.Clamp(pitch, 0.4f, 2.2f);
            src.spatialBlend = spatial;
            if (vol > 0.001f && !src.isPlaying)
                src.Play();
        }

        private void EnsureSources()
        {
            if (_motor == null)
                _motor = MakeSource("FPVMod.Motor", FpvMotorClipFactory.MotorLoop, 0.55f);
            if (_air == null)
                _air = MakeSource("FPVMod.Air", FpvMotorClipFactory.AirLoop, 0.7f);
        }

        private AudioSource MakeSource(string name, AudioClip clip, float spatSpread)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = PossessedSpatial;
            src.dopplerLevel = 0f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 8f;
            src.maxDistance = 420f;
            src.spread = spatSpread * 60f;
            src.priority = 64;
            TryBindMixer(src);
            src.Play();
            return src;
        }

        private static void TryBindMixer(AudioSource src)
        {
            try
            {
                SoundManager? sm = SoundManager.i;
                if (sm == null)
                    return;
                AudioMixerGroup? g = sm.EffectsMixer;
                if (g != null)
                    src.outputAudioMixerGroup = g;
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>Stock bomb whistle would fight FPV motor — mute once.</summary>
        private void MuteVanillaFlightSound()
        {
            if (_vanillaMuted || _missile == null)
                return;

            try
            {
                AudioSource? fs = FpvReflection.GetField<AudioSource>(_missile, "flightSound");
                if (fs != null)
                {
                    fs.Stop();
                    fs.mute = true;
                    fs.enabled = false;
                    _vanillaMuted = true;
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
