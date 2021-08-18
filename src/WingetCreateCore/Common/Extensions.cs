// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Common
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;

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

        /// <summary>
        /// Determines if the object is of dictionary type.
        /// </summary>
        /// <param name="o">Object to be checked.</param>
        /// <returns>Boolean value indicating whether the object is a dictionary type.</returns>
        public static bool IsDictionary(this object o)
        {
            return o is IDictionary &&
                       o.GetType().IsGenericType &&
                       o.GetType().GetGenericTypeDefinition().IsAssignableFrom(typeof(Dictionary<,>));
        }

        /// <summary>
        /// Determines if the type is a List type.
        /// </summary>
        /// <param name="type">Type to be evaluated.</param>
        /// <returns>Boolean value indicating whether the type is a List.</returns>
        public static bool IsList(this Type type)
        {
            return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
            //return value is IList && value.GetType().IsGenericType;
        }

        /// <summary>
        /// Returns the enum member attribute value if one exists.
        /// </summary>
        /// <param name="enumVal">Target enum value.</param>
        /// <returns>Enum member attribute string value.</returns>
        public static string ToEnumAttributeValue(this Enum enumVal)
        {
            var type = enumVal.GetType();
            var memInfo = type.GetMember(enumVal.ToString());
            var attributes = memInfo[0].GetCustomAttributes(typeof(EnumMemberAttribute), false);
            EnumMemberAttribute attributeValue = (attributes.Length > 0) ? (EnumMemberAttribute)attributes[0] : null;
            return attributeValue?.Value ?? enumVal.ToString();
        }

        /// <summary>
        /// Determines if the properties of an object are all equal to null excluding properties with dictionary type if needed.
        /// </summary>
        /// <param name="o">Object to be evaluated.</param>
        /// <returns>Boolean value indicating whether the object is empty.</returns>
        public static bool IsEmptyObject(this object o)
        {
            return !o.GetType().GetProperties().Select(pi => pi.GetValue(o)).Where(value => !value.IsDictionary() && value != null).Any();
        }
    }
}
