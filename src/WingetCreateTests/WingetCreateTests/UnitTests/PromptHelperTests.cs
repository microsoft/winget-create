// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.WingetCreateCLI;
    using Microsoft.WingetCreateCore.Models.Installer;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    /// <summary>
    /// Tests for applying interactive installer edits.
    /// </summary>
    public class PromptHelperTests
    {
        /// <summary>
        /// Verifies partial edits preserve existing fields and replace lists without sharing their elements.
        /// </summary>
        [Test]
        public void ApplyPartialChangesToAllInstallers()
        {
            var edits = new Installer
            {
                InstallerSwitches = new InstallerSwitches { Silent = "new" },
                AppsAndFeaturesEntries = new List<AppsAndFeaturesEntry> { new AppsAndFeaturesEntry { DisplayName = "new" } },
                InstallerAbortsTerminal = false,
            };
            var installers = new List<Installer>
            {
                new Installer { Architecture = Architecture.X64, InstallerSwitches = new InstallerSwitches { Silent = "old", Custom = "keep" }, InstallerAbortsTerminal = true },
                new Installer { Architecture = Architecture.Arm64 },
            };
            typeof(PromptHelper).GetMethod("ApplyChangesToIndividualInstallers", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { edits, installers });

            Assert.That(installers[0].InstallerSwitches.Custom, Is.EqualTo("keep"));
            Assert.That(installers[0].InstallerSwitches.Silent, Is.EqualTo("new"));
            Assert.That(installers[0].InstallerAbortsTerminal, Is.False);
            Assert.That(installers[0].Architecture, Is.EqualTo(Architecture.X64));
            Assert.That(installers[1].Architecture, Is.EqualTo(Architecture.Arm64));
            installers[0].AppsAndFeaturesEntries[0].DisplayName = "changed again";
            Assert.That(installers[1].AppsAndFeaturesEntries[0].DisplayName, Is.EqualTo("new"));
            Assert.That(edits.AppsAndFeaturesEntries[0].DisplayName, Is.EqualTo("new"));
        }

        /// <summary>
        /// Verifies every editable field, including nested properties, reaches each installer.
        /// </summary>
        /// <param name="property">The editable installer property.</param>
        [TestCaseSource(nameof(EditableInstallerProperties))]
        public void ApplyChangesToAllInstallers(PropertyInfo property)
        {
            var edits = new Installer();
            property.SetValue(edits, CreateValue(property.PropertyType));
            var expected = JToken.FromObject(property.GetValue(edits));
            var installers = new List<Installer> { new Installer(), new Installer() };

            typeof(PromptHelper).GetMethod("ApplyChangesToIndividualInstallers", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { edits, installers });

            foreach (var installer in installers)
            {
                var actual = property.GetValue(installer);
                Assert.That(actual, Is.Not.Null, property.Name);
                Assert.That(JToken.DeepEquals(expected, JToken.FromObject(actual)), Is.True, property.Name);
                if (!property.PropertyType.IsValueType && property.PropertyType != typeof(string))
                {
                    Assert.That(actual, Is.Not.SameAs(property.GetValue(edits)), property.Name);
                }
            }
        }

        private static IEnumerable<TestCaseData> EditableInstallerProperties()
        {
            var properties = typeof(Installer).GetProperties();
            var names = (List<string>)typeof(PromptHelper)
                .GetMethod("FilterPropertiesAndCreateSelectionList", BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(typeof(Installer))
                .Invoke(null, new object[] { new Installer(), properties });
            return properties.Where(p => names.Contains(p.Name) && p.Name != nameof(Installer.Architecture))
                .Select(p => new TestCaseData(p).SetArgDisplayNames(p.Name));
        }

        private static object CreateValue(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(string))
            {
                return "test";
            }

            if (type.IsEnum)
            {
                return Enum.GetValues(type).GetValue(0);
            }

            if (type.IsValueType)
            {
                return Convert.ChangeType(1, type);
            }

            var value = Activator.CreateInstance(type);
            if (value is IList list)
            {
                list.Add(CreateValue(type.GetGenericArguments()[0]));
            }
            else
            {
                foreach (var property in type.GetProperties().Where(p => p.CanWrite && p.Name != nameof(Installer.AdditionalProperties)))
                {
                    property.SetValue(value, CreateValue(property.PropertyType));
                }
            }

            return value;
        }
    }
}
