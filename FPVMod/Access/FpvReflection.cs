using System;
using System.Reflection;
using UnityEngine;

namespace FPVMod.Access
{
    internal static class FpvReflection
    {
        internal static FieldInfo? Field(Type type, string name) =>
            type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        internal static void SetField(object target, string name, object? value)
        {
            FieldInfo? f = Field(target.GetType(), name);
            f?.SetValue(target, value);
        }

        internal static T? GetField<T>(object target, string name)
        {
            FieldInfo? f = Field(target.GetType(), name);
            if (f == null)
                return default;
            return (T?)f.GetValue(target);
        }
    }
}
