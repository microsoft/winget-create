﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Text;
    using Newtonsoft.Json;
    using YamlDotNet.Core;
    using YamlDotNet.Core.Events;
    using YamlDotNet.Serialization;
    using YamlDotNet.Serialization.NamingConventions;

    /// <summary>
    /// Provides functionality for the serialization of JSON objects to yaml.
    /// </summary>
    public static class Serialization
    {
        /// <summary>
        /// Gets or sets the application that produced the manifest, will be added to comment header.
        /// </summary>
        public static string ProducedBy { get; set; }

        /// <summary>
        /// Helper to build a YAML serializer.
        /// </summary>
        /// <returns>ISerializer object.</returns>
        public static ISerializer CreateSerializer()
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .WithTypeConverter(new YamlStringEnumConverter())
                .WithEmissionPhaseObjectGraphVisitor(args => new YamlSkipPropertyVisitor(args.InnerVisitor))
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull);
            return serializer.Build();
        }

        /// <summary>
        /// Helper to build a YAML deserializer.
        /// </summary>
        /// <returns>IDeserializer object.</returns>
        public static IDeserializer CreateDeserializer()
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .WithTypeConverter(new YamlStringEnumConverter())
                .IgnoreUnmatchedProperties();
            return deserializer.Build();
        }

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
            var deserializer = Serialization.CreateDeserializer();
            return deserializer.Deserialize<T>(value);
        }

        /// <summary>
        /// Serialize an object to a YAML string.
        /// </summary>
        /// <param name="value">Object to serialize to YAML.</param>
        /// <typeparam name="T">Type of object to serialize.</typeparam>
        /// <returns>Manifest in string value.</returns>
        public static string ToYaml<T>(this T value)
            where T : new()
        {
            var serializer = CreateSerializer();
            string manifestYaml = serializer.Serialize(value);
            StringBuilder serialized = new StringBuilder();
            serialized.AppendLine($"# Created using {ProducedBy}");

            string schemaTemplate = "# yaml-language-server: $schema=https://aka.ms/winget-manifest.{0}.{1}.schema.json";

            switch (value)
            {
                case Models.Singleton.SingletonManifest singletonManifest:
                    serialized.AppendLine(string.Format(schemaTemplate, singletonManifest.ManifestType, singletonManifest.ManifestVersion));
                    break;
                case Models.Version.VersionManifest versionManifest:
                    serialized.AppendLine(string.Format(schemaTemplate, versionManifest.ManifestType, versionManifest.ManifestVersion));
                    break;
                case Models.Installer.InstallerManifest installerManifest:
                    serialized.AppendLine(string.Format(schemaTemplate, installerManifest.ManifestType, installerManifest.ManifestVersion));
                    break;
                case Models.Locale.LocaleManifest localeManifest:
                    serialized.AppendLine(string.Format(schemaTemplate, localeManifest.ManifestType, localeManifest.ManifestVersion));
                    break;
                case Models.DefaultLocale.DefaultLocaleManifest defaultLocaleManifest:
                    serialized.AppendLine(string.Format(schemaTemplate, defaultLocaleManifest.ManifestType, defaultLocaleManifest.ManifestVersion));
                    break;
            }

            serialized.AppendLine();
            serialized.AppendLine(manifestYaml);

            return serialized.ToString();
        }

        /// <summary>
        /// Deserializes yaml to JSON object.
        /// </summary>
        /// <param name="yaml">yaml to deserialize to JSON object.</param>
        /// <returns>Manifest as a JSON object.</returns>
        public static string ConvertYamlToJson(string yaml)
        {
            var deserializer = CreateDeserializer();
            using var reader = new StringReader(yaml);
            var yamlObject = deserializer.Deserialize(reader);
            return JsonConvert.SerializeObject(yamlObject);
        }

        private class YamlStringEnumConverter : IYamlTypeConverter
        {
            public bool Accepts(Type type)
            {
                Type u = Nullable.GetUnderlyingType(type);
                return type.IsEnum || ((u != null) && u.IsEnum);
            }

            public object ReadYaml(IParser parser, Type type)
            {
                Type u = Nullable.GetUnderlyingType(type);
                if (u != null)
                {
                    type = u;
                }

                var parsedEnum = parser.Consume<Scalar>();
                var serializableValues = type.GetMembers()
                    .Select(m => new KeyValuePair<string, MemberInfo>(m.GetCustomAttributes<EnumMemberAttribute>(true).Select(ema => ema.Value).FirstOrDefault(), m))
                    .Where(pa => !string.IsNullOrEmpty(pa.Key)).ToDictionary(pa => pa.Key, pa => pa.Value);
                if (!serializableValues.ContainsKey(parsedEnum.Value))
                {
                    throw new YamlException(parsedEnum.Start, parsedEnum.End, $"Value '{parsedEnum.Value}' not found in enum '{type.Name}'");
                }

                return Enum.Parse(type, serializableValues[parsedEnum.Value].Name);
            }

            public void WriteYaml(IEmitter emitter, object value, Type type)
            {
                var enumMember = type.GetMember(value.ToString()).FirstOrDefault();
                var yamlValue = enumMember?.GetCustomAttributes<EnumMemberAttribute>(true).Select(ema => ema.Value).FirstOrDefault() ?? value.ToString();
                emitter.Emit(new Scalar(yamlValue));
            }
        }

        private class YamlSkipPropertyVisitor : YamlDotNet.Serialization.ObjectGraphVisitors.ChainedObjectGraphVisitor
        {
            public YamlSkipPropertyVisitor(IObjectGraphVisitor<IEmitter> nextVisitor)
                : base(nextVisitor)
            {
            }

            public override bool EnterMapping(IPropertyDescriptor key, IObjectDescriptor value, IEmitter context)
            {
                if (key.Name == "AdditionalProperties")
                {
                    return false;
                }

                return base.EnterMapping(key, value, context);
            }
        }
    }
}
