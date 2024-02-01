// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.WingetCreateCore.Interfaces;
    using Microsoft.WingetCreateCore.Models;
    using Microsoft.WingetCreateCore.Models.DefaultLocale;
    using Microsoft.WingetCreateCore.Models.Installer;
    using Microsoft.WingetCreateCore.Models.Locale;
    using Microsoft.WingetCreateCore.Models.Singleton;
    using Microsoft.WingetCreateCore.Models.Version;
    using Microsoft.WingetCreateCore.Serializers;

    /// <summary>
    /// Provides functionality for the serialization of JSON objects to yaml.
    /// </summary>
    public static class Serialization
    {
        static Serialization()
        {
            // Default to yaml serializer.
            ManifestSerializer = new YamlSerializer();
        }

        /// <summary>
        /// Gets or sets the manifest serializer to be used for serializing output manifest.
        /// </summary>
        public static IManifestSerializer ManifestSerializer { get; set; }

        /// <summary>
        /// Gets implementation types of <see cref="IManifestSerializer"/> available in the current app domain.
        /// </summary>
        public static List<Type> AvailableSerializerTypes { get; } =
            AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => typeof(IManifestSerializer).IsAssignableFrom(p) && !p.IsInterface)
            .ToList();

        /// <summary>
        /// Gets or sets the application that produced the manifest, will be added to comment header.
        /// </summary>
        public static string ProducedBy { get; set; }

        /// <summary>
        /// Deserialize a stream reader into a Manifest object.
        /// </summary>
        /// <typeparam name="T">Manifest type.</typeparam>
        /// <param name="filePath">File path.</param>
        /// <returns>Manifest object populated and validated.</returns>
        public static T DeserializeFromPath<T>(string filePath)
        {
            string text = File.ReadAllText(filePath);
            return DeserializeFromString<T>(text);
        }

        /// <summary>
        /// Deserialize a string into a Manifest object.
        /// </summary>
        /// <typeparam name="T">Manifest type.</typeparam>
        /// <param name="value">Manifest in string value.</param>
        /// <returns>Manifest object populated and validated.</returns>
        public static T DeserializeFromString<T>(string value)
        {
            value = value.Trim();
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Manifest is empty.");
            }

            // Early return if the value is already a json object or array.
            if ((value.StartsWith("{") && value.EndsWith("}")) ||
                (value.StartsWith("[") && value.EndsWith("]")))
            {
                return new JsonSerializer().ManifestDeserialize<T>(value);
            }

            foreach (var serializerType in AvailableSerializerTypes)
            {
                var serializer = (IManifestSerializer)Activator.CreateInstance(serializerType);
                try
                {
                    return serializer.ManifestDeserialize<T>(value);
                }
                catch (Exception)
                {
                    // Ignore exception and try next serializer.
                }
            }

            throw new ArgumentException("Manifest is not in a valid format.");
        }

        /// <summary>
        /// Serialize an object to a manifest string.
        /// </summary>
        /// <param name="value">Object to serialize.</param>
        /// <typeparam name="T">Type of object to serialize.</typeparam>
        /// <param name="omitCreatedByHeader">Value to indicate whether to omit the created by header.</param>
        /// <returns>Manifest in string value.</returns>
        public static string ToManifestString<T>(this T value, bool omitCreatedByHeader = false)
            where T : new()
        {
            return ManifestSerializer.ToManifestString(value, omitCreatedByHeader);
        }

        /// <summary>
        /// Serializes the provided object and returns the serialized string.
        /// </summary>
        /// <param name="value">Object to be serialized.</param>
        /// <returns>Serialized string.</returns>
        public static string Serialize(object value)
        {
            return ManifestSerializer.ManifestSerialize(value);
        }

        /// <summary>
        /// Deserializes a list of manifest strings into their appropriate object models.
        /// </summary>
        /// <param name="manifestContents">List of manifest string contents.</param>
        /// <returns>Manifest object model.</returns>
        public static Manifests DeserializeManifestContents(IEnumerable<string> manifestContents)
        {
            Manifests manifests = new Manifests();

            foreach (string content in manifestContents)
            {
                string trimmedContent = RemoveBom(content);

                ManifestTypeBase baseType = Serialization.DeserializeFromString<ManifestTypeBase>(trimmedContent);

                if (baseType.ManifestType == "singleton")
                {
                    manifests.SingletonManifest = Serialization.DeserializeFromString<SingletonManifest>(trimmedContent);
                }
                else if (baseType.ManifestType == "version")
                {
                    manifests.VersionManifest = Serialization.DeserializeFromString<VersionManifest>(trimmedContent);
                }
                else if (baseType.ManifestType == "defaultLocale")
                {
                    manifests.DefaultLocaleManifest = Serialization.DeserializeFromString<DefaultLocaleManifest>(trimmedContent);
                }
                else if (baseType.ManifestType == "locale")
                {
                    manifests.LocaleManifests.Add(Serialization.DeserializeFromString<LocaleManifest>(trimmedContent));
                }
                else if (baseType.ManifestType == "installer")
                {
                    manifests.InstallerManifest = Serialization.DeserializeFromString<InstallerManifest>(trimmedContent);
                }
            }

            return manifests;
        }

        /// <summary>
        /// Set the manifest serializer based on the provided value.
        /// </summary>
        /// <param name="outputFormat">String value denoting the output manifest format.</param>
        /// <exception cref="ArgumentException">Exception thrown when an invalid serialization type is provided.</exception>
        public static void SetManifestSerializer(string outputFormat)
        {
            // Set the serializer based on the provided value
            ManifestSerializer = outputFormat.ToLower() switch
            {
                "yaml" => new YamlSerializer(),
                "json" => new JsonSerializer(),
                _ => throw new ArgumentException($"Invalid serialization type: {outputFormat}"),
            };
        }

        /// <summary>
        /// Removes Byte Order Marker (BOM) prefix if exists in string.
        /// </summary>
        /// <param name="value">String to fix.</param>
        /// <returns>String without BOM prefix.</returns>
        private static string RemoveBom(string value)
        {
            string bomMarkUtf8 = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble());
            return value.StartsWith(bomMarkUtf8, StringComparison.OrdinalIgnoreCase) ? value.Remove(0, bomMarkUtf8.Length) : value;
        }
    }
}
