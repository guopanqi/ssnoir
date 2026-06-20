#nullable enable
using System;
using Schemy;

namespace SSNoir.Scripting
{
    // Small coercion helpers shared by the Scheme bridge code, so the same
    // "symbol-or-string id" and "numeric -> int" unpacking isn't re-spelled
    // at every native function.
    public static class SchemeValue
    {
        // A Scheme identifier argument may arrive as a Symbol or a string.
        public static string AsId(object? value)
        {
            return value is Symbol sym ? sym.AsString : value?.ToString() ?? "";
        }

        // Schemy numbers can be int, double, or long depending on how they were produced.
        public static int ToInt(object value)
        {
            if (value is int i) return i;
            if (value is double d) return (int)d;
            if (value is long l) return (int)l;
            return Convert.ToInt32(value);
        }
    }
}
