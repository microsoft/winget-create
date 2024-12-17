// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Serializers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Text;
    using Microsoft.WingetCreateCore.Interfaces;
    using Newtonsoft.Json;
    using YamlDotNet.Core;
    using YamlDotNet.Core.Events;
    using YamlDotNet.Serialization;
    using YamlDotNet.Serialization.EventEmitters;
    using YamlDotNet.Serialization.NamingConventions;
    using YamlDotNet.Serialization.TypeInspectors;

    /// <summary>
    /// Serializer class for YAML.
    /// </summary>
    public class YamlSerializer : IManifestSerializer
    {
        /// <inheritdoc/>
        public string AssociatedFileExtension => ".yaml";

        /// <inheritdoc/>
        public T ManifestDeserialize<T>(string value)
        {
            var deserializer = CreateYamlDeserializer();
            return deserializer.Deserialize<T>(value);
        }

        /// <inheritdoc/>
        public string ManifestSerialize(object value)
        {
            var serializer = CreateYamlSerializer();
            return serializer.Serialize(value);
        }

        /// <inheritdoc/>
        public string ToManifestString<T>(T value, bool omitCreatedByHeader = false)
            where T : new()
        {
            var serializer = CreateYamlSerializer();
            string manifestYaml = serializer.Serialize(value);
            StringBuilder serialized = new StringBuilder();

            if (!omitCreatedByHeader)
            {
                serialized.AppendLine($"# Created using {Serialization.ProducedBy}");
            }

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
        public string ConvertYamlToJson(string yaml)
        {
            var deserializer = CreateYamlDeserializer();
            using var reader = new StringReader(yaml);
            var yamlObject = deserializer.Deserialize(reader);
            return JsonConvert.SerializeObject(yamlObject);
        }

        /// <summary>
        /// Helper to build a YAML serializer.
        /// </summary>
        /// <returns>ISerializer object.</returns>
        private static ISerializer CreateYamlSerializer()
        {
            var serializer = new SerializerBuilder()
                .WithQuotingNecessaryStrings()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
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
        private static IDeserializer CreateYamlDeserializer()
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .WithTypeConverter(new YamlStringEnumConverter())
                .WithTypeInspector(inspector => new AliasTypeInspector(inspector))
                .IgnoreUnmatchedProperties();
            return deserializer.Build();
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

            public override string GetEnumName(Type enumType, string name) => this.innerTypeDescriptor.GetEnumName(enumType, name);

            public override string GetEnumValue(object enumValue) => this.innerTypeDescriptor.GetEnumValue(enumValue);

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

            public object ReadYaml(IParser parser, Type type, ObjectDeserializer objectDeserializer)
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

            public void WriteYaml(IEmitter emitter, object value, Type type, ObjectSerializer objectSerializer)
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

            public override bool EnterMapping(IPropertyDescriptor key, IObjectDescriptor value, IEmitter context, ObjectSerializer objectSerializer)
            {
                if (key.Name == "AdditionalProperties")
                {
                    return false;
                }

                return base.EnterMapping(key, value, context, objectSerializer);
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
