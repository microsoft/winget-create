// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Interfaces
{
    /// <summary>
    /// Interface for manifest serialization.
    /// </summary>
    public interface IManifestSerializer
    {
        /// <summary>
        /// Gets the file extension associated with the serializer.
        /// </summary>
        string AssociatedFileExtension { get; }

        /// <summary>
        /// Serializes the provided object and returns the serialized string.
        /// </summary>
        /// <param name="value">Object to be serialized.</param>
        /// <returns>Serialized string.</returns>
        string ManifestSerialize(object value);

        /// <summary>
        /// Deserialize a string into a Manifest object.
        /// </summary>
        /// <typeparam name="T">Manifest type.</typeparam>
        /// <param name="value">Manifest in string value.</param>
        /// <returns>Manifest object populated and validated.</returns>
        T ManifestDeserialize<T>(string value);

        /// <summary>
        /// Serialize an object to a manifest string.
        /// </summary>
        /// <param name="value">Object to serialize.</param>
        /// <typeparam name="T">Type of object to serialize.</typeparam>
        /// <param name="omitCreatedByHeader">Value to indicate whether to omit the created by header.</param>
        /// <returns>Manifest in string value.</returns>
        string ToManifestString<T>(T value, bool omitCreatedByHeader = false)
            where T : new();
    }
}
