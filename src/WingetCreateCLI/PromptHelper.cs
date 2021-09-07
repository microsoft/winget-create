// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Reflection;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Common;
    using Microsoft.WingetCreateCore.Models.Installer;
    using Newtonsoft.Json;
    using Sharprompt;

    /// <summary>
    /// Provides functionality for prompting the user for input to obtain field values for a manifest.
    /// </summary>
    public static class PromptHelper
    {
        /// <summary>
        /// List of strings representing the optional fields that should not be editable.
        /// </summary>
        private static readonly string[] NonEditableOptionalFields = new[]
        {
            nameof(InstallerType),
            nameof(InstallerManifest.Channel),
            nameof(Installer.InstallerUrl),
            nameof(Installer.InstallerSha256),
            nameof(Installer.SignatureSha256),
            nameof(Installer.Platform),
            nameof(Installer.ProductCode),
            nameof(Installer.PackageFamilyName),
            nameof(Installer.AdditionalProperties),
            nameof(Installer.Capabilities),
            nameof(Installer.RestrictedCapabilities),
        };

        private static readonly string[] MsixExclusionList = new[]
        {
            nameof(Installer.Scope),
            nameof(Installer.InstallerSwitches),
            nameof(Installer.InstallerSuccessCodes),
            nameof(Installer.UpgradeBehavior),
            nameof(Installer.InstallModes),
        };

        private static readonly string[] MsixInclusionList = new[]
        {
            nameof(Installer.Capabilities),
            nameof(Installer.RestrictedCapabilities),
        };

        /// <summary>
        /// Displays all properties of a model as a navigational menu selection with some additional filtering logic based on InstallerType.
        /// </summary>
        /// <typeparam name="T">Model type.</typeparam>
        /// <param name="model">Instance of object model.</param>
        /// <param name="exitMenuWord">Exit keyword to be shown to the user to exit the navigational menu.</param>
        public static void PromptPropertiesWithMenu<T>(T model, string exitMenuWord)
        {
            Console.Clear();

            var properties = model.GetType().GetProperties().ToList();
            var optionalProperties = properties.Where(p =>
                p.GetCustomAttribute<RequiredAttribute>() == null &&
                p.GetCustomAttribute<JsonPropertyAttribute>() != null).ToList();

            var fieldList = properties
                .Select(property => property.Name)
                .Where(pName => !NonEditableOptionalFields.Any(field => field == pName)).ToList();

            var installerTypeProperty = model.GetType().GetProperty(nameof(InstallerType));
            if (installerTypeProperty != null)
            {
                var installerTypeValue = installerTypeProperty.GetValue(model);
                if (installerTypeValue != null)
                {
                    var installerType = (InstallerType)installerTypeValue;
                    if (installerType == InstallerType.Msi || installerType == InstallerType.Exe)
                    {
                        fieldList.Add(nameof(Installer.ProductCode));
                    }
                    else if (installerType == InstallerType.Msix || installerType == InstallerType.Appx)
                    {
                        fieldList = fieldList.Where(pName => !MsixExclusionList.Any(field => field == pName)).ToList();
                        fieldList.AddRange(MsixInclusionList);
                    }
                }
            }

            fieldList.Add(exitMenuWord);

            while (true)
            {
                Utils.WriteLineColored(ConsoleColor.Green, Resources.FilterMenuItems_Message);
                var selectedField = Prompt.Select(Resources.SelectPropertyToEdit_Message, fieldList);
                Console.Clear();

                if (selectedField == exitMenuWord)
                {
                    break;
                }

                var selectedProperty = properties.First(p => p.Name == selectedField);
                PromptPropertyAndSetValue(model, selectedField, selectedProperty.GetValue(model));
            }
        }

        /// <summary>
        /// Generic method for prompting for a property value of any type.
        /// </summary>
        /// <typeparam name="T">Type of the property instance.</typeparam>
        /// <param name="model">Object model to be modified.</param>
        /// <param name="memberName">Name of the selected property field.</param>
        /// <param name="instance">Instance value of the property.</param>
        /// <param name="minimum">Minimum number of entries required for the list.</param>
        /// <param name="validationModel">Object model to be validated against if the target field differs from what is specified in the model (i.e. NewCommand.InstallerUrls).</param>
        /// <param name="validationName">Name of the property field to be used for looking up validation constraints if the target field name differs from what specified in the model.</param>
        public static void PromptPropertyAndSetValue<T>(object model, string memberName, T instance, int minimum = 0, object validationModel = null, string validationName = null)
        {
            Type instanceType = typeof(T);
            string message = string.Format(Resources.FieldValueIs_Message, memberName);
            Console.WriteLine(Resources.ResourceManager.GetString($"{memberName}_KeywordDescription"));

            if (instanceType == typeof(object))
            {
                // if the instance type is an object, obtain the type from the property.
                instanceType = model.GetType().GetProperty(memberName).PropertyType;
            }

            if (instanceType.IsEnumerable())
            {
                // elementType is needed so that Prompt.List prompts for type T and not type List<T>
                Type elementType = instanceType.GetGenericArguments().SingleOrDefault();
                var mi = typeof(PromptHelper).GetMethod(nameof(PromptHelper.PromptList));
                var generic = mi.MakeGenericMethod(elementType);
                generic.Invoke(instance, new[] { message, model, memberName, instance, minimum, validationModel, validationName });
            }
            else
            {
                var mi = typeof(PromptHelper).GetMethod(nameof(PromptHelper.PromptValue));
                var generic = mi.MakeGenericMethod(instanceType);
                generic.Invoke(instance, new[] { message, model, memberName, instance });
            }
        }

        /// <summary>
        /// Displays a prompt to the user to enter in a value for the selected field (excludes List properties) and sets the value of the property.
        /// </summary>
        /// <typeparam name="T">Type of the property instance.</typeparam>
        /// <param name="message">Prompt message to be displayed to the user.</param>
        /// <param name="model">Object model to be modified.</param>
        /// <param name="memberName">Name of the selected property field.</param>
        /// <param name="instance">Instance value of the property.</param>
        public static void PromptValue<T>(string message, object model, string memberName, T instance)
        {
            var property = model.GetType().GetProperty(memberName);
            Type instanceType = typeof(T);
            Type elementType;

            if (instanceType == typeof(string))
            {
                var result = Prompt.Input<T>(message, property.GetValue(model), new[] { FieldValidation.ValidateProperty(model, memberName, instance) });
                property.SetValue(model, result);
            }
            else if (instanceType.IsEnum)
            {
                PromptEnum(model, property, instanceType, true, message, instance.ToString());
            }
            else if ((elementType = Nullable.GetUnderlyingType(instanceType)) != null)
            {
                if (elementType.IsEnum)
                {
                    PromptEnum(model, property, elementType, false, message: message);
                }
            }
            else if (property.PropertyType.IsClass)
            {
                // Handles InstallerSwitches and Dependencies which both have their own unique class.
                var newInstance = (T)Activator.CreateInstance(instanceType);
                var initialValue = (T)property.GetValue(model) ?? newInstance;
                PromptSubfieldProperties(initialValue, property, model);
            }
        }

        /// <summary>
        /// Prompts the user for input for a field that takes in a list a values and sets the value of the property.
        /// </summary>
        /// <typeparam name="T">Type of the property instance.</typeparam>
        /// <param name="message">Prompt message to be displayed to the user.</param>
        /// <param name="model">Object model to be modified.</param>
        /// <param name="memberName">Name of the selected property field.</param>
        /// <param name="instance">Instance value of the property.</param>
        /// <param name="minimum">Minimum number of entries required for the list.</param>
        /// <param name="validationModel">Object model to be validated against if the target field differs from what is specified in the model (i.e. NewCommand.InstallerUrls).</param>
        /// <param name="validationName">Name of the property field to be used for looking up validation constraints if the target field name differs from what specified in the model.</param>
        public static void PromptList<T>(string message, object model, string memberName, List<T> instance, int minimum = 0, object validationModel = null, string validationName = null)
        {
            var property = model.GetType().GetProperty(memberName);
            Type instanceType = typeof(T);

            if (instanceType.IsEnum)
            {
                // Handles List<Enum> properties
                var value = Prompt.MultiSelect(message, Enum.GetNames(instanceType), minimum: 0);

                if (value.Any())
                {
                    Type genericListType = typeof(List<>).MakeGenericType(instanceType);
                    var enumList = (IList)Activator.CreateInstance(genericListType);

                    foreach (var item in value)
                    {
                        var enumItem = Enum.Parse(instanceType, item);
                        enumList.Add(enumItem);
                    }

                    property.SetValue(model, enumList);
                }
            }
            else if (instanceType == typeof(PackageDependencies))
            {
                // Handles List<PackageDependency>
                List<PackageDependencies> packageDependencies = (List<PackageDependencies>)property.GetValue(model) ?? new List<PackageDependencies>();

                PromptForItemList(packageDependencies);
                if (packageDependencies.Any())
                {
                    property.SetValue(model, packageDependencies);
                }
                else
                {
                    property.SetValue(model, null);
                }
            }
            else
            {
                // Handles all other cases such as List<string>, List<int>, etc.
                var maxLengthAttribute = property.GetCustomAttribute<MaxLengthAttribute>();
                int maxEntries = int.MaxValue;
                if (maxLengthAttribute != null)
                {
                    maxEntries = maxLengthAttribute.Length;
                }

                IEnumerable<T> value;
                if (validationModel == null && string.IsNullOrEmpty(validationName))
                {
                    // Fields that take in a list don't have restrictions on values, only restrictions on the number of entries.
                    value = Prompt.List<T>(message, minimum: minimum, maximum: maxEntries);
                }
                else
                {
                    // Special case when the validation name differs from what is specified in the model (i.e. NewCommand.InstallerUrls needs to use Installer.InstallerUrl for validation)
                    value = Prompt.List<T>(message, minimum: minimum, maximum: maxEntries, validators: new[] { FieldValidation.ValidateProperty(validationModel, validationName, instance) });
                }

                if (!value.Any())
                {
                    value = null;
                }

                property.SetValue(model, value);
            }
        }

        private static void PromptEnum(object model, PropertyInfo property, Type enumType, bool required, string message, string defaultValue = null)
        {
            var enumList = Enum.GetNames(enumType);

            if (!required)
            {
                enumList.Append(Resources.None_MenuItem);
            }

            var value = Prompt.Select(message, enumList, defaultValue: defaultValue);
            if (value != Resources.None_MenuItem)
            {
                property.SetValue(model, Enum.Parse(enumType, value));
            }
        }

        private static void PromptForItemList<T>(List<T> items)
            where T : new()
        {
            var serializer = Serialization.CreateSerializer();
            string selection = string.Empty;
            while (selection != Resources.Back_MenuItem)
            {
                Console.Clear();
                if (items.Any())
                {
                    Logger.InfoLocalized(nameof(Resources.DisplayPreviewOfItems), typeof(T).Name);
                    string serializedString = serializer.Serialize(items);
                    Console.WriteLine(serializedString);
                    Console.WriteLine();
                }

                selection = Prompt.Select(Resources.SelectAction_Message, new[] { Resources.Add_MenuItem, Resources.RemoveLastEntry_MenuItem, Resources.Back_MenuItem });
                if (selection == Resources.Add_MenuItem)
                {
                    T newItem = new T();
                    PromptPropertiesWithMenu(newItem, Resources.Done_MenuItem);

                    // Ignore dictionary types as we don't want to take into account the AdditionalProperties field.
                    var properties = newItem.GetType().GetProperties().Select(p => p).Where(p => !p.GetValue(newItem).IsDictionary());

                    // Check that all values are present before appending to list.
                    if (!properties.Any(p => p.GetValue(newItem) == null))
                    {
                        items.Add(newItem);
                    }
                }
                else if (selection == Resources.RemoveLastEntry_MenuItem)
                {
                    if (items.Any())
                    {
                        items.RemoveAt(items.Count - 1);
                    }
                }
            }
        }

        private static void PromptSubfieldProperties<T>(T field, PropertyInfo property, object model)
        {
            PromptPropertiesWithMenu(field, Resources.None_MenuItem);
            if (field.IsEmptyObject())
            {
                property.SetValue(model, null);
            }
            else
            {
                property.SetValue(model, field);
            }
        }
    }
}
