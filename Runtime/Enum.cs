using System;
using System.Diagnostics;

namespace Neonalig.Core
{
    public static class Enum<TEnum> where TEnum : struct, Enum
    {
        static readonly TEnum[] _values = (TEnum[])Enum.GetValues(typeof(TEnum));

        /// <summary>
        /// Gets all defined values of the enum type.
        /// </summary>
        /// <returns>An array of all enum values.</returns>
        public static TEnum[] GetValues()
        {
            return _values;
        }

        /// <summary>
        /// Checks if the specified value is defined in the enum type.
        /// </summary>
        /// <param name="value">The enum value to check.</param>
        /// <returns>>True if the value is defined in the enum; otherwise, false.</returns>
        public static bool Contains(TEnum value)
        {
            return Array.IndexOf(_values, value) >= 0;
        }
    }
}
