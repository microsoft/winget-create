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
    using Microsoft.WingetCreateCore.Models.DefaultLocale;
    using Microsoft.WingetCreateCore.Models.Installer;
    using Sharprompt;

    /// <summary>
    /// Provides functionality for prompting the user for input to obtain field values for a manifest.
    /// </summary>
    public static class PromptHelper
    {
        private static readonly string[] NonEditableRequiredFields = new[]
        {
            nameof(InstallerManifest.PackageIdentifier),
            nameof(InstallerManifest.PackageVersion),
            nameof(InstallerManifest.ManifestType),
            nameof(InstallerManifest.ManifestVersion),
        };

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
            nameof(Installer.ReleaseDate),  // Ignore due to bad model conversion from schema (ReleaseDate should be string type).
            nameof(Agreement.Agreement1),   // Ignore due to bad model conversion from schema (Agreement1 property name should be Agreement).
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
        /// <param name="modelName">If a non-null string is provided, a menu item to display a preview of the model will be added to the selection list.</param>
        public static void PromptPropertiesWithMenu<T>(T model, string exitMenuWord, string modelName = null)
        {
            Console.Clear();
            var properties = model.GetType().GetProperties();
            var fieldList = FilterPropertiesAndCreateSelectionList(model, properties);

            if (!string.IsNullOrEmpty(modelName))
            {
                fieldList.Add(Resources.DisplayPreview_MenuItem);
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
                else if (selectedField == Resources.DisplayPreview_MenuItem)
                {
                    Console.Clear();
                    Console.WriteLine();
                    Logger.InfoLocalized(nameof(Resources.DisplayPreviewOfItems), modelName);
                    Console.WriteLine(Serialization.Serialize(model));
                    Console.WriteLine();
                }
                else if (selectedField == nameof(InstallerManifest.Installers))
                {
                    DisplayInstallersAsMenuSelection(model as InstallerManifest);
                }
                else
                {
                    var selectedProperty = properties.First(p => p.Name == selectedField);
                    PromptPropertyAndSetValue(model, selectedField, selectedProperty.GetValue(model));
                }
            }
        }

        /// <summary>
        /// Displays a prompt selection menu for adding and editing items to a list of objects.
        /// </summary>
        /// <typeparam name="T">Model type.</typeparam>
        /// <param name="model">Instance of object model.</param>
        /// <param name="memberName">Name of the selected property field.</param>
        public static void PromptListOfClassType<T>(object model, string memberName)
            where T : new()
        {
            var property = model.GetType().GetProperty(memberName);
            List<T> objectList = (List<T>)property.GetValue(model);
            objectList ??= new List<T>();

            string name = objectList.GetType().GetGenericArguments().Single().Name;

            while (true)
            {
                Console.Clear();
                int count = 1;
                Dictionary<string, T> selectionMap = new Dictionary<string, T>();
                List<string> selectionList = new List<string>();
                objectList.ForEach(item =>
                    {
                        string entryName = $"{name}{count}";
                        selectionMap.Add(entryName, item);
                        selectionList.Add(entryName);
                        count++;
                    });

                if (objectList.Any())
                {
                    selectionList.AddRange(new[] { Resources.AddNewItem_MenuItem, Resources.RemoveLastItem_MenuItem, Resources.SaveAndExit_MenuItem });
                }
                else
                {
                    selectionList.AddRange(new[] { Resources.AddNewItem_MenuItem, Resources.SaveAndExit_MenuItem });
                }

                Console.WriteLine($"[{memberName}]: " + Resources.ResourceManager.GetString($"{memberName}_KeywordDescription"));
                var selectedItem = Prompt.Select(Resources.SelectItemToEdit_Message, selectionList);

                if (selectedItem == Resources.SaveAndExit_MenuItem)
                {
                    break;
                }
                else if (selectedItem == Resources.AddNewItem_MenuItem)
                {
                    var item = new T();
                    PromptPropertiesWithMenu(item, Resources.SaveAndExit_MenuItem);
                    objectList.Add(item);
                }
                else if (selectedItem == Resources.RemoveLastItem_MenuItem)
                {
                    objectList.RemoveAt(objectList.Count - 1);
                }
                else
                {
                    var item = selectionMap[selectedItem];
                    PromptPropertiesWithMenu(item, Resources.SaveAndExit_MenuItem);
                }
            }

            if (!objectList.Any())
            {
                property.SetValue(model, null);
            }
            else
            {
                property.SetValue(model, objectList);
            }
        }

        /// <summary>
        /// Displays all installers from an Installer manifest as a selection menu.
        /// </summary>
        /// <param name="installerManifest">Installer manifest model.</param>
        public static void DisplayInstallersAsMenuSelection(InstallerManifest installerManifest)
        {
            Console.Clear();

            while (true)
            {
                List<string> selectionList = GenerateInstallerSelectionList(installerManifest.Installers, out Dictionary<string, Installer> installerSelectionMap);
                var selectedItem = Prompt.Select(Resources.SelectInstallerToEdit_Message, selectionList);

                if (selectedItem == Resources.SaveAndExit_MenuItem)
                {
                    break;
                }
                else if (selectedItem == Resources.AllInstallers_MenuItem)
                {
                    Installer installerCopy = new Installer();
                    PromptPropertiesWithMenu(installerCopy, Resources.SaveAndExit_MenuItem);
                    ApplyChangesToIndividualInstallers(installerCopy, installerManifest.Installers);
                }
                else if (selectedItem == Resources.DisplayPreview_MenuItem)
                {
                    Console.Clear();
                    Console.WriteLine();
                    Logger.InfoLocalized(nameof(Resources.DisplayPreviewOfSelectedInstaller_Message));
                    Console.WriteLine(Serialization.Serialize(installerManifest));
                    Console.WriteLine();
                }
                else
                {
                    Installer selectedInstaller = installerSelectionMap[selectedItem];
                    PromptPropertiesWithMenu(selectedInstaller, Resources.SaveAndExit_MenuItem, selectedItem.Split(':')[0]);
                }
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
                if (elementType.IsNonStringClassType())
                {
                    var mi = typeof(PromptHelper).GetMethod(nameof(PromptHelper.PromptListOfClassType));
                    var generic = mi.MakeGenericMethod(elementType);
                    generic.Invoke(instance, new[] { model, memberName });
                }
                else
                {
                    var mi = typeof(PromptHelper).GetMethod(nameof(PromptHelper.PromptList));
                    var generic = mi.MakeGenericMethod(elementType);
                    generic.Invoke(instance, new[] { message, model, memberName, instance, minimum, validationModel, validationName });
                }
            }
            else
            {
                if (instanceType.IsEnum)
                {
                    // Default enum values need to be converted from long/int to Enum type to ensure type consistency for the generic method call.
                    instance = (T)Enum.ToObject(instanceType, instance);
                }

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
                string result = Prompt.Input<string>(message, property.GetValue(model), null, new[] { FieldValidation.ValidateProperty(model, memberName, instance) });

                if (!string.IsNullOrEmpty(result))
                {
                    property.SetValue(model, result.Trim());
                }
            }
            else if (instanceType == typeof(long))
            {
                long result = Prompt.Input<long>(message, property.GetValue(model), null, new[] { FieldValidation.ValidateProperty(model, memberName, instance) });

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
                else if (elementType == typeof(bool))
                {
                    PromptBool(model, property, message);
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
        public static void PromptList<T>(string message, object model, string memberName, IEnumerable<T> instance, int minimum = 0, object validationModel = null, string validationName = null)
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
                    // In update scenarios, if an older value exists then use that value instead of setting the property to null.
                    var existingValue = model.GetType().GetProperty(property.Name).GetValue(model);
                    value = existingValue != null ? (IEnumerable<T>)existingValue : null;
                }
                else
                {
                    // Trim values if we have List<string>
                    if (instanceType == typeof(string))
                    {
                        value = (IEnumerable<T>)value.Select(v => v.ToString().Trim()).ToList();
                    }
                }

                property.SetValue(model, value);
            }
        }

        /// <summary>
        /// Creates a filtered list of strings representing the fields to be shown in a selection menu. Filters out
        /// non-editable fields as well as fields that are not relevant based on the installer type if present.
        /// </summary>
        /// <typeparam name="T">Object type.</typeparam>
        /// <param name="model">Object model.</param>
        /// <param name="properties">Array of model properties.</param>
        /// <returns>List of strings representing the properties to be shown in the selection menu.</returns>
        private static List<string> FilterPropertiesAndCreateSelectionList<T>(T model, PropertyInfo[] properties)
        {
            var fieldList = properties
                .Select(property => property.Name)
                .Where(pName =>
                    !NonEditableOptionalFields.Any(field => field == pName) &&
                    !NonEditableRequiredFields.Any(field => field == pName)).ToList();

            // Filter out fields if an installerType is present
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

            return fieldList;
        }

        /// <summary>
        /// Creates a list of strings representing the installer nodes to be shown in a selection menu.
        /// </summary>
        /// <param name="installers">List of installers.</param>
        /// <param name="installerSelectionMap">Installer dictionary that maps the installer menu item string to the installer object model.</param>
        /// <returns>List of strings representing the installers to be shown in the selection menu. </returns>
        private static List<string> GenerateInstallerSelectionList(List<Installer> installers, out Dictionary<string, Installer> installerSelectionMap)
        {
            installerSelectionMap = new Dictionary<string, Installer>();
            int index = 1;
            foreach (Installer installer in installers)
            {
                var installerTuple = string.Join(" | ", new[]
                    {
                        installer.Architecture.ToEnumAttributeValue(),
                        installer.InstallerType?.ToEnumAttributeValue(),
                        installer.Scope?.ToEnumAttributeValue(),
                        installer.InstallerLocale,
                        installer.InstallerUrl,
                    }.Where(s => !string.IsNullOrEmpty(s)));

                var installerMenuItem = string.Format(Resources.InstallerSelection_MenuItem, index, installerTuple);
                installerSelectionMap.Add(installerMenuItem, installer);
                index++;
            }

            List<string> selectionList = new List<string>() { Resources.AllInstallers_MenuItem };
            selectionList.AddRange(installerSelectionMap.Keys);
            selectionList.AddRange(new[] { Resources.DisplayPreview_MenuItem, Resources.SaveAndExit_MenuItem });
            return selectionList;
        }

        /// <summary>
        /// Applies all installerCopy changes to each installer in the List of installers.
        /// </summary>
        /// <param name="installerCopy">Installer object model with new changes.</param>
        /// <param name="installers">List of installers receiving the new changes.</param>
        private static void ApplyChangesToIndividualInstallers(Installer installerCopy, List<Installer> installers)
        {
            // Skip architecture as the default value when instantiated is x86.
            var modifiedFields = installerCopy.GetType().GetProperties()
                .Select(prop => prop)
                .Where(pi =>
                    pi.GetValue(installerCopy) != null &&
                    pi.Name != nameof(Installer.Architecture) &&
                    pi.Name != nameof(Installer.AdditionalProperties));

            foreach (var field in modifiedFields)
            {
                foreach (Installer installer in installers)
                {
                    var fieldValue = field.GetValue(installerCopy);
                    var prop = installer.GetType().GetProperty(field.Name);
                    if (prop.PropertyType.IsValueType)
                    {
                        prop.SetValue(installer, fieldValue);
                    }
                    else if (fieldValue is IList list)
                    {
                        prop.SetValue(installer, list.DeepClone());
                    }
                    else if (fieldValue is Dependencies dependencies)
                    {
                        ApplyDependencyChangesToInstaller(dependencies, installer);
                    }
                }
            }
        }

        /// <summary>
        /// Clones any non-null property values of the dependencies object and assigns them to the provided installer object.
        /// </summary>
        /// <param name="dependencies">Dependencies object with new values.</param>
        /// <param name="installer">Installer object to assign new changes to.</param>
        private static void ApplyDependencyChangesToInstaller(Dependencies dependencies, Installer installer)
        {
            var modifiedFields = dependencies.GetType().GetProperties()
                .Select(prop => prop)
                .Where(pi => pi.GetValue(dependencies) != null);

            foreach (var field in modifiedFields.Where(f => f.Name != nameof(Installer.AdditionalProperties)))
            {
                var fieldValue = field.GetValue(dependencies);
                installer.Dependencies ??= new Dependencies();
                var prop = installer.Dependencies.GetType().GetProperty(field.Name);

                if (fieldValue is IList list)
                {
                    prop.SetValue(installer.Dependencies, list.DeepClone());
                }
            }
        }

        private static void PromptBool(object model, PropertyInfo property, string message)
        {
            bool[] boolList = new[] { true, false };
            var selectedValue = Prompt.Select(message, boolList);
            property.SetValue(model, selectedValue);
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
            string selection = string.Empty;
            while (selection != Resources.Back_MenuItem)
            {
                Console.Clear();
                if (items.Any())
                {
                    Logger.InfoLocalized(nameof(Resources.DisplayPreviewOfItems), typeof(T).Name);
                    string serializedString = Serialization.Serialize(items);
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
            PromptPropertiesWithMenu(field, Resources.SaveAndExit_MenuItem);
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
