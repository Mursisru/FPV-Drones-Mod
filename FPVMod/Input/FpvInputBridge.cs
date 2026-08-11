using Rewired;
using UnityEngine;

namespace FPVMod.Input
{
    internal struct FpvInputSample
    {
        internal float Pitch;
        internal float Roll;
        internal float Yaw;
        internal float Throttle;
    }

    internal static class FpvInputBridge
    {
        internal static FpvInputSample Sample { get; private set; }
        internal static float LagBlend { get; set; } = 1f;

        private static readonly FpvInputRingBuffer _lag = new(12);
        private static FpvInputSample _raw;

        internal static void Poll(Rewired.Player player)
        {
            if (player == null)
            {
                Sample = default;
                return;
            }

            int inv = PlayerSettings.virtualJoystickInvertPitch ? -1 : 1;
            _raw.Pitch = Mathf.Clamp(player.GetAxis("Pitch") * inv, -1f, 1f);
            _raw.Roll = Mathf.Clamp(player.GetAxis("Roll"), -1f, 1f);
            _raw.Yaw = Mathf.Clamp(player.GetAxis("Yaw"), -1f, 1f);
            _raw.Throttle = ReadThrottle(player);

            _lag.Push(_raw);
            Sample = _lag.Sample(LagBlend);
        }

        internal static void Freeze()
        {
            _lag.Clear();
            Sample = default;
        }

        internal static void ApplyCompressed(byte pitch, byte roll, byte yaw, byte throttle)
        {
            Sample = new FpvInputSample
            {
                Pitch = AxisFromByte(pitch),
                Roll = AxisFromByte(roll),
                Yaw = AxisFromByte(yaw),
                Throttle = throttle / 255f
            };
        }

        private static float AxisFromByte(byte b) => b / 127.5f - 1f;

        private static float ReadThrottle(Rewired.Player player)
        {
            float t = Mathf.Clamp(player.GetAxisRaw("Throttle"), -1f, 1f);
            return Mathf.Clamp01((t + 1f) * 0.5f);
        }
    }

    internal sealed class FpvInputRingBuffer
    {
        private readonly FpvInputSample[] _buf;
        private int _head;
        private int _count;

        internal FpvInputRingBuffer(int size) => _buf = new FpvInputSample[size];

        internal void Push(FpvInputSample s)
        {
            _buf[_head] = s;
            _head = (_head + 1) % _buf.Length;
            if (_count < _buf.Length)
                _count++;
        }

        internal FpvInputSample Sample(float blend)
        {
            if (_count == 0)
                return default;
            int idx = (_head - 1 - Mathf.RoundToInt((1f - blend) * (_count - 1)) + _buf.Length) % _buf.Length;
            return _buf[idx];
        }

        internal void Clear()
        {
            _head = 0;
            _count = 0;
        }
    }
}
