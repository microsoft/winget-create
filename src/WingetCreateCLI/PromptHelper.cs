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
            nameof(Installer.ProductCode),
            nameof(Installer.PackageFamilyName),
            nameof(Installer.AdditionalProperties),
        };

        /// <summary>
        /// Displays all properties of a model as a navigational menu selection with some additional filtering logic based on InstallerType.
        /// </summary>
        /// <typeparam name="T">Model type.</typeparam>
        /// <param name="model">Instance of object model.</param>
        /// <param name="exitMenuWord">Exit keyword to be shown to the user to exit the navigational menu.</param>
        public static void DisplayPropertiesAsMenuSelection<T>(T model, string exitMenuWord)
        {
            Console.Clear();

            var properties = model.GetType().GetProperties().ToList();
            var optionalProperties = properties.Where(p =>
                p.GetCustomAttribute<RequiredAttribute>() == null &&
                p.GetCustomAttribute<JsonPropertyAttribute>() != null).ToList();

            var fieldList = properties
                .Select(property => property.Name)
                .Where(pName => !NonEditableOptionalFields.Any(field => field == pName))
                .Append(exitMenuWord)
                .ToList();

            var installerTypeProperty = model.GetType().GetProperty(nameof(InstallerType));
            if (installerTypeProperty != null)
            {
                var installerType = installerTypeProperty.GetValue(model);
                if (installerType != null)
                {
                    if ((InstallerType)installerType == InstallerType.Msi)
                    {
                        fieldList.Add(nameof(Installer.ProductCode));
                    }
                    else if ((InstallerType)installerType == InstallerType.Msix)
                    {
                        fieldList.Add(nameof(Installer.PackageFamilyName));
                    }
                }
            }

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

                PromptAndSetPropertyValue(model, selectedField);
            }
        }

        /// <summary>
        /// Displays a prompt to the user to enter in a value for the selected field.
        /// </summary>
        /// <param name="model">Object model to be modified.</param>
        /// <param name="memberName">Name of the selected property field.</param>
        /// <param name="minimum">Specifies the minimum number of entries if the property is a list.</param>
        /// <param name="validationModel">Object model to be validated against if the target field differs from what is specified in the model (i.e. NewCommand.InstallerUrls).</param>
        /// <param name="validationName">Name of the property field to be used for looking up validation constraints if the target field name differs from what specified in the model.</param>
        public static void PromptAndSetPropertyValue(object model, string memberName, int minimum = 0, object validationModel = null, string validationName = null)
        {
            if (string.IsNullOrEmpty(validationName))
            {
                validationName = memberName;
            }

            if (validationModel == null)
            {
                validationModel = model;
            }

            string message = string.Format(Resources.FieldValueIs_Message, memberName);
            Console.WriteLine(Resources.ResourceManager.GetString($"{memberName}_KeywordDescription"));
            var property = model.GetType().GetProperty(memberName);
            var defaultValue = property.GetValue(model);
            Type propertyType = property.PropertyType;
            Type elementType;

            if (propertyType == typeof(string))
            {
                var result = Prompt.Input<string>(message, property.GetValue(model), new[] { FieldValidation.ValidateProperty(validationModel, validationName, defaultValue) });
                property.SetValue(model, result);
            }
            else if (propertyType.IsEnum)
            {
                PromptEnum(model, property, defaultValue.GetType(), true, message, defaultValue.ToString());
            }
            else if ((elementType = Nullable.GetUnderlyingType(propertyType)) != null)
            {
                if (elementType.IsEnum)
                {
                    PromptEnum(model, property, elementType, false, message: message);
                }
            }
            else if (propertyType.IsList())
            {
                elementType = propertyType.GetGenericArguments().SingleOrDefault();
                if (elementType == typeof(string) || typeof(IEnumerable<string>).IsAssignableFrom(propertyType))
                {
                    var value = Prompt.List<string>(message, minimum: minimum, validators: new[] { FieldValidation.ValidateProperty(validationModel, validationName, defaultValue) });
                    if (!value.Any())
                    {
                        value = null;
                    }

                    property.SetValue(model, value);
                }
                else if (elementType == typeof(int))
                {
                    // The only field that takes in List<int> is InstallerSuccessCodes, which only has constraints on the number of entries.
                    // TODO: Add validation for checking the number of entries in the list.
                    var value = Prompt.List<int>(message, minimum: minimum);
                    property.SetValue(model, value);
                }
                else if (elementType.IsEnum)
                {
                    var value = Prompt.MultiSelect(message, Enum.GetNames(elementType), minimum: 0);

                    if (value.Any())
                    {
                        Type genericListType = typeof(List<>).MakeGenericType(elementType);
                        var enumList = (IList)Activator.CreateInstance(genericListType);

                        foreach (var item in value)
                        {
                            var enumItem = Enum.Parse(elementType, item);
                            enumList.Add(enumItem);
                        }

                        property.SetValue(model, enumList);
                    }
                }
                else if (elementType == typeof(PackageDependencies))
                {
                    List<PackageDependencies> packageDependencies = (List<PackageDependencies>)property.GetValue(model) ?? new List<PackageDependencies>();

                    PromptForPackageDependencies(packageDependencies);
                    if (packageDependencies.Any())
                    {
                        property.SetValue(model, packageDependencies);
                    }
                    else
                    {
                        property.SetValue(model, null);
                    }
                }
            }
            else
            {
                if (property.PropertyType == typeof(InstallerSwitches))
                {
                    InstallerSwitches installerSwitches = (InstallerSwitches)property.GetValue(model) ?? new InstallerSwitches();
                    PromptSubfieldProperties(installerSwitches, property, model);
                }
                else if (property.PropertyType == typeof(Dependencies))
                {
                    Dependencies dependencies = (Dependencies)property.GetValue(model) ?? new Dependencies();
                    PromptSubfieldProperties(dependencies, property, model);
                }
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

        private static void PromptForPackageDependencies(List<PackageDependencies> packageDependencies)
        {
            var serializer = Serialization.CreateSerializer();
            string selection = string.Empty;
            while (selection != Resources.Back_MenuItem)
            {
                Console.Clear();
                if (packageDependencies.Any())
                {
                    Logger.InfoLocalized(nameof(Resources.DisplayPreviewOfPackageDependencies_Message), nameof(PackageDependencies));
                    string serializedString = serializer.Serialize(packageDependencies);
                    Console.WriteLine(serializedString);
                    Console.WriteLine();
                }

                selection = Prompt.Select(Resources.SelectAction_Message, new[] { Resources.Add_MenuItem, Resources.RemoveLastEntry_MenuItem, Resources.Back_MenuItem });
                if (selection == Resources.Add_MenuItem)
                {
                    PackageDependencies newDependency = new PackageDependencies();
                    DisplayPropertiesAsMenuSelection(newDependency, Resources.Done_MenuItem);
                    if (!string.IsNullOrEmpty(newDependency.PackageIdentifier) && !string.IsNullOrEmpty(newDependency.MinimumVersion))
                    {
                        packageDependencies.Add(newDependency);
                    }
                }
                else if (selection == Resources.RemoveLastEntry_MenuItem)
                {
                    if (packageDependencies.Any())
                    {
                        packageDependencies.RemoveAt(packageDependencies.Count - 1);
                    }
                }
            }
        }

        private static void PromptSubfieldProperties<T>(T field, PropertyInfo property, object model)
        {
            DisplayPropertiesAsMenuSelection(field, Resources.None_MenuItem);
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
