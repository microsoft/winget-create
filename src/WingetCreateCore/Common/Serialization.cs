// Copyright (c) Microsoft. All rights reserved.
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
    using Microsoft.WingetCreateCore.Models;
    using Microsoft.WingetCreateCore.Models.DefaultLocale;
    using Microsoft.WingetCreateCore.Models.Installer;
    using Microsoft.WingetCreateCore.Models.Locale;
    using Microsoft.WingetCreateCore.Models.Singleton;
    using Microsoft.WingetCreateCore.Models.Version;
    using Newtonsoft.Json;
    using YamlDotNet.Core;
    using YamlDotNet.Core.Events;
    using YamlDotNet.Serialization;
    using YamlDotNet.Serialization.EventEmitters;
    using YamlDotNet.Serialization.NamingConventions;
    using YamlDotNet.Serialization.TypeInspectors;

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
                .WithDefaultScalarStyle(ScalarStyle.SingleQuoted)
                .WithTypeConverter(new YamlStringEnumConverter())
                .WithEmissionPhaseObjectGraphVisitor(args => new YamlSkipPropertyVisitor(args.InnerVisitor))
                .WithEventEmitter(nextEmitter => new MultilineScalarFlowStyleEmitter(nextEmitter))
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
                .WithTypeInspector(inspector => new AliasTypeInspector(inspector))
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
            serialized.Append(manifestYaml);

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

        /// <summary>
        /// Serializes the provided object and returns the serialized string.
        /// </summary>
        /// <param name="value">Object to be serialized.</param>
        /// <returns>Serialized string.</returns>
        public static string Serialize(object value)
        {
            var serializer = CreateSerializer();
            return serializer.Serialize(value);
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
        /// Removes Byte Order Marker (BOM) prefix if exists in string.
        /// </summary>
        /// <param name="value">String to fix.</param>
        /// <returns>String without BOM prefix.</returns>
        private static string RemoveBom(string value)
        {
            string bomMarkUtf8 = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble());
            return value.StartsWith(bomMarkUtf8, StringComparison.OrdinalIgnoreCase) ? value.Remove(0, bomMarkUtf8.Length) : value;
        }

        /// <summary>
        /// Custom TypeInspector to priorize properties that have a defined YamlMemberAttribute for custom override.
        /// </summary>
        private class AliasTypeInspector : TypeInspectorSkeleton
        {
            private readonly ITypeInspector innerTypeDescriptor;

            public AliasTypeInspector(ITypeInspector innerTypeDescriptor)
            {
                this.innerTypeDescriptor = innerTypeDescriptor;
            }

            /// <summary>
            /// Because certain properties were generated incorrectly, we needed to create custom fields for those properties.
            /// Therefore to resolve naming conflicts during deserialization, we prioritize fields that have the YamlMemberAttribute defined
            /// as that attribute indicates an override.
            /// </summary>
            public override IEnumerable<IPropertyDescriptor> GetProperties(Type type, object container)
            {
                var propertyDescriptors = this.innerTypeDescriptor.GetProperties(type, container);
                var aliasDefinedProps = type.GetProperties().ToList()
                    .Where(p =>
                    {
                        var yamlMemberAttribute = p.GetCustomAttribute<YamlMemberAttribute>();
                        return yamlMemberAttribute != null && !string.IsNullOrEmpty(yamlMemberAttribute.Alias);
                    })
                    .ToList();

                if (aliasDefinedProps.Any())
                {
                    var overriddenProps = propertyDescriptors
                        .Where(prop => aliasDefinedProps.Any(aliasProp =>
                            prop.Name == aliasProp.GetCustomAttribute<YamlMemberAttribute>().Alias && // Use Alias name (ex. ReleaseDate) instead of property name (ex. ReleaseDateString).
                            prop.Type != aliasProp.PropertyType))
                        .ToList();

                    // Remove overridden properties from the returned list of deserializable properties.
                    return propertyDescriptors
                        .Where(prop => !overriddenProps.Any(overridenProp =>
                            prop.Name == overridenProp.Name &&
                            prop.Type == overridenProp.Type))
                        .ToList();
                }
                else
                {
                    return propertyDescriptors;
                }
            }
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

        /// <summary>
        /// A custom emitter for YamlDotNet which ensures all multiline fields use a <see cref="ScalarStyle.Literal"/>.
        /// </summary>
        private class MultilineScalarFlowStyleEmitter : ChainedEventEmitter
        {
            public MultilineScalarFlowStyleEmitter(IEventEmitter nextEmitter)
                : base(nextEmitter)
            {
            }

            public override void Emit(ScalarEventInfo eventInfo, IEmitter emitter)
            {
                if (typeof(string).IsAssignableFrom(eventInfo.Source.Type))
                {
                    var outString = eventInfo.Source.Value as string;
                    if (!string.IsNullOrEmpty(outString))
                    {
                        bool isMultiLine = new[] { '\r', '\n', '\x85', '\x2028', '\x2029' }.Any(outString.Contains);
                        if (isMultiLine)
                        {
                            eventInfo = new ScalarEventInfo(eventInfo.Source) { Style = ScalarStyle.Literal };
                        }
                    }
                }

                this.nextEmitter.Emit(eventInfo, emitter);
            }
        }
    }
}
