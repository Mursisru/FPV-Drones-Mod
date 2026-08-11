using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FPVMod.Access
{
    /// <summary>Vanilla TargetCam fields — mirror URP / IR exposure (MC TargetCamAccess port).</summary>
    internal static class FpvTargetCamAccess
    {
        private const BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly Dictionary<FieldKey, FieldInfo?> FieldCache = new Dictionary<FieldKey, FieldInfo?>(16);
        private static FieldInfo? _irModeField;
        private static bool _irModeResolved;

        internal static TargetCam? GetTargetCam(Component aircraft) =>
            GetFieldCached<TargetCam>(aircraft, "targetCam");

        internal static Camera? GetCam(TargetCam instance) =>
            GetFieldCached<Camera>(instance, "cam");

        internal static bool IsIrMode(TargetCam instance)
        {
            if (!_irModeResolved)
            {
                _irModeField = typeof(TargetCam).GetField("IRMode", InstanceAny);
                _irModeResolved = true;
            }
            return _irModeField?.GetValue(instance) is bool ir && ir;
        }

        internal static bool TryGetColorAdjustments(TargetCam instance, out ColorAdjustments? adjustments)
        {
            adjustments = GetFieldCached<ColorAdjustments>(instance, "colorAdjustments");
            return adjustments != null;
        }

        internal static bool TryGetVanillaIrSnapshot(out bool irMode, out float postExposure, out float contrast)
        {
            irMode = false;
            postExposure = 0f;
            contrast = 1f;

            if (!GameManager.GetLocalAircraft(out Aircraft aircraft))
                return false;

            TargetCam? targetCam = GetTargetCam(aircraft);
            if (targetCam == null)
                return false;

            irMode = IsIrMode(targetCam);
            if (!TryGetColorAdjustments(targetCam, out ColorAdjustments? adjustments) || adjustments == null)
                return false;

            postExposure = adjustments.postExposure.value;
            contrast = adjustments.contrast.value;
            return true;
        }

        internal static Camera? TryGetLocalTargetCam()
        {
            if (!GameManager.GetLocalAircraft(out Aircraft aircraft))
                return null;
            TargetCam? tc = GetTargetCam(aircraft);
            return tc != null ? GetCam(tc) : null;
        }

        private static T? GetFieldCached<T>(Component instance, string name) where T : class
        {
            if (instance == null)
                return null;

            Type type = instance.GetType();
            var key = new FieldKey(type, name);
            if (!FieldCache.TryGetValue(key, out FieldInfo? field))
            {
                field = type.GetField(name, InstanceAny);
                FieldCache[key] = field;
            }
            return field?.GetValue(instance) as T;
        }

        private static T? GetFieldCached<T>(TargetCam instance, string name) where T : class
        {
            if (instance == null)
                return null;

            Type type = typeof(TargetCam);
            var key = new FieldKey(type, name);
            if (!FieldCache.TryGetValue(key, out FieldInfo? field))
            {
                field = type.GetField(name, InstanceAny);
                FieldCache[key] = field;
            }
            return field?.GetValue(instance) as T;
        }

        private readonly struct FieldKey : IEquatable<FieldKey>
        {
            private readonly Type _type;
            private readonly string _name;

            internal FieldKey(Type type, string name)
            {
                _type = type;
                _name = name;
            }

            public bool Equals(FieldKey other) =>
                ReferenceEquals(_type, other._type) && _name == other._name;

            public override bool Equals(object? obj) =>
                obj is FieldKey other && Equals(other);

            public override int GetHashCode() =>
                (_type.GetHashCode() * 397) ^ (_name != null ? _name.GetHashCode() : 0);
        }
    }
}
