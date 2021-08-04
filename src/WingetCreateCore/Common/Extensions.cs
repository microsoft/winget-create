// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Common
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Functionality for manipulating data related to the Manifest object model.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Converts string value to Enum Type.
        /// </summary>
        /// <typeparam name="T">Enum Type.</typeparam>
        /// <param name="value">String to be converted to Enum type.</param>
        /// <returns>Converted Enum, or null if conversion fails.</returns>
        public static T? ToEnumOrDefault<T>(this string value)
            where T : struct
        {
            return Enum.TryParse(value, true, out T result) ? (T?)result : null;
        }

        /// <summary>
        /// Converts object to Int type.
        /// </summary>
        /// <param name="o">Object to be converted to Int type.</param>
        /// <returns>Converted Int, or null if conversion fails.</returns>
        public static int? ToIntOrDefault(this object o)
        {
            return int.TryParse(o?.ToString(), out int result) ? result : (int?)null;
        }

        /// <summary>
        /// Returns a new string in which all occurences of a specified string in the current instance are removed.
        /// </summary>
        /// <param name="value">String to be modified.</param>
        /// <param name="toRemove">String to be removed.</param>
        /// <returns>A string that is equivalent to the current string except that all instances of toRemove are removed.</returns>
        public static string Remove(this string value, string toRemove)
        {
            return value.Replace(toRemove, string.Empty);
        }

        /// <summary>
        /// Compares strings using the ordinal ignore case.
        /// </summary>
        /// <param name="a">The first string to compare, or null.</param>
        /// <param name="b">The second string to compare, or null.</param>
        /// <returns>true if both strings are equivalent; otherwise, false.</returns>
        public static bool EqualsIC(this string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        ///// <summary>
        ///// Determines if an object is a List type.
        ///// </summary>
        ///// <param name="o">Object to be determined as a list type.</param>
        ///// <returns>A boolean value indicating whether the object is a generic list type.</returns>
        //public static bool IsGenericList(this object o)
        //{
        //    var oType = o.GetType();
        //    return oType.IsGenericType && (oType.GetGenericTypeDefinition() == typeof(List<>));
        //}
    }
}
