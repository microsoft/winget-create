// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Serializers
{
    using System;
    using Microsoft.WingetCreateCore.Interfaces;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Serializer class for JSON.
    /// </summary>
    public class JsonSerializer : IManifestSerializer
    {
        /// <inheritdoc/>
        public string AssociatedFileExtension => ".json";

        /// <inheritdoc/>
        public T ManifestDeserialize<T>(string value)
        {
            return JsonConvert.DeserializeObject<T>(value);
        }

        /// <inheritdoc/>
        public string ManifestSerialize(object value)
        {
            return JsonConvert.SerializeObject(value, Formatting.Indented);
        }

        /// <inheritdoc/>
        public string ToManifestString<T>(T value, bool omitCreatedByHeader = false)
            where T : new()
        {
            var serializer = new Newtonsoft.Json.JsonSerializer { NullValueHandling = NullValueHandling.Ignore };
            var jsonObject = JObject.FromObject(value, serializer);
            string schemaTemplate = "https://aka.ms/winget-manifest.{0}.{1}.schema.json";
            JToken existingSchemaProperty = jsonObject.Property("$schema");

            switch (value)
            {
                case Models.Singleton.SingletonManifest singletonManifest:
                    if (existingSchemaProperty != null)
                    {
                        jsonObject.Property("$schema").Value = string.Format(schemaTemplate, singletonManifest.ManifestType, singletonManifest.ManifestVersion);
                    }
                    else
                    {
                        jsonObject.AddFirst(new JProperty("$schema", string.Format(schemaTemplate, singletonManifest.ManifestType, singletonManifest.ManifestVersion)));
                    }

                    break;
                case Models.Version.VersionManifest versionManifest:
                    if (existingSchemaProperty != null)
                    {
                        jsonObject.Property("$schema").Value = string.Format(schemaTemplate, versionManifest.ManifestType, versionManifest.ManifestVersion);
                    }
                    else
                    {
                        jsonObject.AddFirst(new JProperty("$schema", string.Format(schemaTemplate, versionManifest.ManifestType, versionManifest.ManifestVersion)));
                    }

                    break;
                case Models.Installer.InstallerManifest installerManifest:
                    if (existingSchemaProperty != null)
                    {
                        jsonObject.Property("$schema").Value = string.Format(schemaTemplate, installerManifest.ManifestType, installerManifest.ManifestVersion);
                    }
                    else
                    {
                        jsonObject.AddFirst(new JProperty("$schema", string.Format(schemaTemplate, installerManifest.ManifestType, installerManifest.ManifestVersion)));
                    }

                    break;
                case Models.Locale.LocaleManifest localeManifest:
                    if (existingSchemaProperty != null)
                    {
                        jsonObject.Property("$schema").Value = string.Format(schemaTemplate, localeManifest.ManifestType, localeManifest.ManifestVersion);
                    }
                    else
                    {
                        jsonObject.AddFirst(new JProperty("$schema", string.Format(schemaTemplate, localeManifest.ManifestType, localeManifest.ManifestVersion)));
                    }

                    break;
                case Models.DefaultLocale.DefaultLocaleManifest defaultLocaleManifest:
                    if (existingSchemaProperty != null)
                    {
                        jsonObject.Property("$schema").Value = string.Format(schemaTemplate, defaultLocaleManifest.ManifestType, defaultLocaleManifest.ManifestVersion);
                    }
                    else
                    {
                        jsonObject.AddFirst(new JProperty("$schema", string.Format(schemaTemplate, defaultLocaleManifest.ManifestType, defaultLocaleManifest.ManifestVersion)));
                    }

                    break;
            }

            return jsonObject.ToString(Formatting.Indented) + Environment.NewLine;
        }
    }
}
