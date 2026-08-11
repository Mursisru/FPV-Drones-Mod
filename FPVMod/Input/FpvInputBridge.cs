using Rewired;
using UnityEngine;

namespace FPVMod.Input
{
    internal struct FpvInputSample
    {
        internal float Pitch;
        internal float Roll;
        internal float Yaw;
        /// <summary>Collective 0..1 after idle cut — true 0 = motors off.</summary>
        internal float Throttle;
    }

    /// <summary>
    /// Aircraft sticks + FPV collective. Bottom deadzone forces motors OFF (must fall).
    /// </summary>
    internal static class FpvInputBridge
    {
        private const float VjRadius = 150f;
        /// <summary>Bottom fraction of absolute stick travel = hard cut (no residual hover).</summary>
        private const float AbsoluteIdleCut = 0.35f;
        private const float RelativeSlewUp = 1.2f;
        private const float RelativeSlewDown = 2.5f; // dump collective fast

        internal static FpvInputSample Sample { get; private set; }

        private static Vector3 _vjPos;
        private static float _collective01;

        internal static void ResetSession()
        {
            _vjPos = Vector3.zero;
            _collective01 = 0f;
            Sample = default;
        }

        internal static void Freeze()
        {
            Sample = new FpvInputSample
            {
                Pitch = 0f,
                Roll = 0f,
                Yaw = 0f,
                Throttle = Sample.Throttle
            };
        }

        internal static void Poll(Rewired.Player? player)
        {
            player ??= GameManager.playerInput ?? ReInput.players?.GetPlayer(0);
            if (player == null)
            {
                Sample = default;
                return;
            }

            float inv = PlayerSettings.virtualJoystickInvertPitch ? -1f : 1f;
            float pitch = 0f;
            float roll = 0f;
            float yaw = 0f;

            if (PlayerSettings.virtualJoystickEnabled)
            {
                if (!player.GetButton("Free Look"))
                {
                    float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                    _vjPos += PlayerSettings.virtualJoystickSensitivity * dt * 30f
                              * new Vector3(
                                  player.GetAxis("Pan View"),
                                  -inv * player.GetAxis("Tilt View"),
                                  0f);
                    _vjPos = Vector3.ClampMagnitude(_vjPos, VjRadius);
                }
                _vjPos = Vector3.Lerp(_vjPos, Vector3.zero, PlayerSettings.virtualJoystickCentering * 2f * Time.deltaTime);

                if (!DynamicMap.mapMaximized && !RadialMenuMain.IsInUse())
                {
                    pitch = -_vjPos.y / VjRadius;
                    roll = _vjPos.x / VjRadius;
                }

                TrySyncFlightHudJoystick();
            }
            else
            {
                _vjPos = Vector3.zero;
            }

            pitch += player.GetAxis("Pitch");
            roll += player.GetAxis("Roll");
            yaw += player.GetAxis("Yaw");

            Sample = new FpvInputSample
            {
                Pitch = Mathf.Clamp(pitch, -1f, 1f),
                Roll = Mathf.Clamp(roll, -1f, 1f),
                Yaw = Mathf.Clamp(yaw, -1f, 1f),
                Throttle = PollCollective(player)
            };
        }

        internal static void ApplyCompressed(byte pitch, byte roll, byte yaw, byte throttle)
        {
            float t = throttle / 255f;
            // Same idle cut on net path.
            if (t < AbsoluteIdleCut)
                t = 0f;
            else
                t = (t - AbsoluteIdleCut) / (1f - AbsoluteIdleCut);

            Sample = new FpvInputSample
            {
                Pitch = pitch / 127.5f - 1f,
                Roll = roll / 127.5f - 1f,
                Yaw = yaw / 127.5f - 1f,
                Throttle = t
            };
        }

        private static float PollCollective(Rewired.Player player)
        {
            float raw = Mathf.Clamp(player.GetAxisRaw("Throttle"), -1f, 1f);

            if (PlayerSettings.throttleUseRelative)
            {
                float dir = Mathf.Abs(raw) > 0.08f ? Mathf.Sign(raw) : 0f;
                float slew = dir < 0f ? RelativeSlewDown : RelativeSlewUp;
                _collective01 = Mathf.Clamp01(_collective01 + dir * Time.deltaTime * slew);
                // Explicit cut keys / bottom: if holding down long enough already at 0.
                return _collective01 <= 0.01f ? 0f : _collective01;
            }

            // Absolute: -1..1 → 0..1, then expand idle so "near bottom" = motors OFF.
            // Stick center (raw≈0) is NOT hover — only upper travel after cut gives thrust.
            float u = Mathf.Clamp01((raw + 1f) * 0.5f);
            if (u <= AbsoluteIdleCut)
            {
                _collective01 = 0f;
                return 0f;
            }

            _collective01 = (u - AbsoluteIdleCut) / (1f - AbsoluteIdleCut);
            return _collective01;
        }

        private static void TrySyncFlightHudJoystick()
        {
            try
            {
                FlightHud? hud = SceneSingleton<FlightHud>.i;
                if (hud == null || hud.virtualJoystickPos == null)
                    return;
                if (!hud.virtualJoystickPos.gameObject.activeSelf)
                    hud.virtualJoystickPos.gameObject.SetActive(true);
                hud.SetVirtualJoystick(_vjPos);
            }
            catch
            {
                // ignore
            }
        }
    }
}
