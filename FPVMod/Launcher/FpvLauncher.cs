using UnityEngine;

namespace FPVMod.Launcher
{
    internal enum FpvLauncherState
    {
        Ready,
        Cooldown,
        Empty
    }

    internal sealed class FpvLauncher : MonoBehaviour
    {
        internal int Ammo { get; private set; } = FpvConstants.LauncherCapacity;
        internal float CooldownRemaining { get; private set; }
        internal FpvLauncherState State => GetState();

        private void Update()
        {
            if (CooldownRemaining > 0f)
                CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);
        }

        internal FpvLauncherState GetState()
        {
            if (Ammo <= 0)
                return FpvLauncherState.Empty;
            if (CooldownRemaining > 0f)
                return FpvLauncherState.Cooldown;
            return FpvLauncherState.Ready;
        }

        internal bool CanLaunch() => GetState() == FpvLauncherState.Ready;

        internal bool TryConsumeLaunch()
        {
            if (!CanLaunch())
                return false;
            Ammo--;
            CooldownRemaining = FpvConstants.LauncherCooldownSec;
            return true;
        }

        internal void AddAmmo(int count)
        {
            if (count <= 0)
                return;
            Ammo = Mathf.Min(FpvConstants.LauncherCapacity, Ammo + count);
        }

        internal Unit OwnerUnit => GetComponentInParent<Unit>() ?? GetComponent<Unit>()!;
    }
}
