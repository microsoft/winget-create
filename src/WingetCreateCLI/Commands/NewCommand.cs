// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using CommandLine;
    using CommandLine.Text;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCLI.Telemetry;
    using Microsoft.WingetCreateCLI.Telemetry.Events;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Models;
    using Microsoft.WingetCreateCore.Models.DefaultLocale;
    using Microsoft.WingetCreateCore.Models.Installer;
    using Microsoft.WingetCreateCore.Models.Version;
    using Sharprompt;

    /// <summary>
    /// Command to launch a wizard that prompt users for information to generate a new manifest.
    /// </summary>
    [Verb("new", HelpText = "NewCommand_HelpText", ResourceType = typeof(Resources))]
    public class NewCommand : BaseCommand
    {
        /// <summary>
        /// The url path to the manifest documentation site.
        /// </summary>
        private const string ManifestDocumentationUrl = "https://github.com/microsoft/winget-cli/blob/master/doc/ManifestSpecv1.0.md";

        /// <summary>
        /// Installer types for which we can trust that the detected architecture is correct, so don't need to prompt the user to confirm.
        /// </summary>
        private static readonly InstallerType[] ReliableArchitectureInstallerTypes = new[] { InstallerType.Msi, InstallerType.Msix, InstallerType.Appx };

        /// <summary>
        /// Gets the usage examples for the New command.
        /// </summary>
        [Usage(ApplicationAlias = ProgramApplicationAlias)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example(Resources.Example_NewCommand_StartFromScratch, new NewCommand { });
                yield return new Example(Resources.Example_NewCommand_DownloadInstaller, new NewCommand { InstallerUrl = "<installerURL>" });
                yield return new Example(Resources.Example_NewCommand_SaveLocallyOrSubmit, new NewCommand
                {
                    InstallerUrl = "<InstallerUrl>",
                    OutputDir = "<OutputDirectory>",
                    GitHubToken = "<GitHubPersonalAccessToken>",
                });
            }
        }

        /// <summary>
        /// Gets or sets the installer URL used for downloading and parsing the installer file.
        /// </summary>
        [Value(0, MetaName = "InstallerUrl", Required = false, HelpText = "InstallerUrl_HelpText", ResourceType = typeof(Resources))]
        public string InstallerUrl { get; set; }

        /// <summary>
        /// Gets or sets the outputPath where the generated manifest file should be saved to.
        /// </summary>
        [Option('o', "out", Required = false, HelpText = "OutputDirectory_HelpText", ResourceType = typeof(Resources))]
        public string OutputDir { get; set; }

        /// <summary>
        /// Gets or sets the package file used for parsing and extracting relevant installer metadata.
        /// </summary>
        public string PackageFile { get; set; }

        /// <summary>
        /// Executes the new command flow.
        /// </summary>
        /// <returns>Boolean representing success or fail of the command.</returns>
        public override async Task<bool> Execute()
        {
            CommandExecutedEvent commandEvent = new CommandExecutedEvent
            {
                Command = nameof(NewCommand),
                InstallerUrl = this.InstallerUrl,
                HasGitHubToken = !string.IsNullOrEmpty(this.GitHubToken),
            };

            try
            {
                Prompt.Symbols.Done = new Symbol(string.Empty, string.Empty);
                Prompt.Symbols.Prompt = new Symbol(string.Empty, string.Empty);

                Manifests manifests = new Manifests();

                if (string.IsNullOrEmpty(this.InstallerUrl))
                {
                    this.InstallerUrl = PromptProperty(
                        new Installer(),
                        this.InstallerUrl,
                        nameof(Installer.InstallerUrl));
                }

                this.PackageFile = await DownloadPackageFile(this.InstallerUrl);

                if (string.IsNullOrEmpty(this.PackageFile))
                {
                    return false;
                }

                if (!PackageParser.ParsePackage(this.PackageFile, this.InstallerUrl, manifests))
                {
                    Logger.ErrorLocalized(nameof(Resources.PackageParsing_Error));
                    return false;
                }

                Console.WriteLine(Resources.NewCommand_Header);
                Console.WriteLine();
                Logger.InfoLocalized(nameof(Resources.ManifestDocumentation_HelpText), ManifestDocumentationUrl);
                Console.WriteLine();
                Console.WriteLine(Resources.NewCommand_Description);
                Console.WriteLine();

                Logger.DebugLocalized(nameof(Resources.EnterFollowingFields_Message));

                do
                {
                    PromptRequiredProperties(manifests.VersionManifest);
                    PromptRequiredProperties(manifests.InstallerManifest, manifests.VersionManifest);
                    PromptRequiredProperties(manifests.DefaultLocaleManifest, manifests.VersionManifest);
                    Console.WriteLine();
                    DisplayManifestPreview(manifests);
                }
                while (Prompt.Confirm(Resources.ConfirmManifestCreation_Message));

                if (string.IsNullOrEmpty(this.OutputDir))
                {
                    this.OutputDir = Directory.GetCurrentDirectory();
                }

                string manifestDirectoryPath = SaveManifestDirToLocalPath(manifests, this.OutputDir);

                if (!ValidateManifest(manifestDirectoryPath) ||
                    !Prompt.Confirm(Resources.ConfirmGitHubSubmitManifest_Message) ||
                    !await this.SetAndCheckGitHubToken())
                {
                    Console.WriteLine();
                    Logger.WarnLocalized(nameof(Resources.SkippingPullRequest_Message));
                    return false;
                }

                return commandEvent.IsSuccessful = await this.GitHubSubmitManifests(
                    manifests,
                    this.GitHubToken);
            }
            finally
            {
                TelemetryManager.Log.WriteEvent(commandEvent);
            }
        }

        private static void PromptRequiredProperties<T>(T manifest, VersionManifest versionManifest = null)
        {
            var properties = manifest.GetType().GetProperties().ToList();
            var requiredProperties = properties.Where(p => p.GetCustomAttribute<RequiredAttribute>() != null).ToList();

            foreach (var property in requiredProperties)
            {
                if (property.PropertyType.IsGenericType)
                {
                    // Generic logic for handling nested object models
                    Type itemType = property.GetValue(manifest).GetType().GetGenericArguments().Single();

                    if (itemType.Name == nameof(Installer))
                    {
                        PromptInstallerProperties(manifest, property);
                    }
                }
                else if (property.PropertyType.IsValueType || property.PropertyType == typeof(string))
                {
                    if (property.Name == nameof(VersionManifest.ManifestType) || property.Name == nameof(VersionManifest.ManifestVersion))
                    {
                        continue;
                    }

                    if ((property.Name == nameof(VersionManifest.PackageIdentifier)) && versionManifest != null)
                    {
                        property.SetValue(manifest, versionManifest.PackageIdentifier);
                        continue;
                    }

                    if (property.Name == nameof(VersionManifest.PackageVersion) && versionManifest != null)
                    {
                        property.SetValue(manifest, versionManifest.PackageVersion);
                        continue;
                    }

                    if (property.Name == nameof(DefaultLocaleManifest.PackageLocale) && versionManifest != null)
                    {
                        property.SetValue(manifest, versionManifest.DefaultLocale);
                        continue;
                    }

                    var currentValue = property.GetValue(manifest);
                    var result = PromptProperty(manifest, currentValue, property.Name);
                    property.SetValue(manifest, result);
                    Logger.Trace($"Property [{property.Name}] set to the value [{result}]");
                }
            }
        }

        private static void PromptInstallerProperties<T>(T manifest, PropertyInfo property)
        {
            List<Installer> installers = new List<Installer>((ICollection<Installer>)property.GetValue(manifest));
            Installer singleInstaller = installers.FirstOrDefault();
            var installerProperties = singleInstaller.GetType().GetProperties().ToList();

            var requiredInstallerProperties = installerProperties
                .Where(p => Attribute.IsDefined(p, typeof(RequiredAttribute))).ToList();

            foreach (var requiredProperty in requiredInstallerProperties)
            {
                var currentValue = requiredProperty.GetValue(singleInstaller);

                // Only prompt if the value isn't already set, or if it's the Architecture property and we don't trust the parser to have gotten it correct for this InstallerType.
                if (currentValue == null ||
                    (requiredProperty.Name == nameof(Installer.Architecture) && !ReliableArchitectureInstallerTypes.Contains(singleInstaller.InstallerType.Value)))
                {
                    var result = PromptProperty(singleInstaller, currentValue, requiredProperty.Name);
                    requiredProperty.SetValue(singleInstaller, result);
                }

                // If we know the installertype is EXE, prompt the user for installer switches (silent and silentwithprogress)
                if (requiredProperty.Name == nameof(Installer.InstallerType))
                {
                    if ((InstallerType)currentValue == InstallerType.Exe)
                    {
                        PromptInstallerSwitchesForExe(manifest);
                    }
                }
            }
        }

        private static void PromptInstallerSwitchesForExe<T>(T manifest)
        {
            InstallerSwitches installerSwitches = new InstallerSwitches();

            var silentSwitchResult = PromptProperty(installerSwitches, installerSwitches.Silent, nameof(InstallerSwitches.Silent));
            var silentWithProgressSwitchResult = PromptProperty(installerSwitches, installerSwitches.SilentWithProgress, nameof(InstallerSwitches.SilentWithProgress));

            bool updateSwitches = false;

            if (!string.IsNullOrEmpty(silentSwitchResult))
            {
                installerSwitches.Silent = silentSwitchResult;
                updateSwitches = true;
            }

            if (!string.IsNullOrEmpty(silentWithProgressSwitchResult))
            {
                installerSwitches.SilentWithProgress = silentWithProgressSwitchResult;
                updateSwitches = true;
            }

            if (updateSwitches)
            {
                manifest.GetType().GetProperty(nameof(InstallerSwitches)).SetValue(manifest, installerSwitches);
            }
        }

        private static T PromptProperty<T>(object model, T property, string memberName, string message = null)
        {
            message ??= $"[{memberName}] " +
            Resources.ResourceManager.GetString($"{memberName}_KeywordDescription") ?? memberName;
            if (property?.GetType().IsEnum ?? false)
            {
                // For enums, we want to call Prompt.Select<T>, specifically the overload that takes 4 parameters
                var generic = typeof(Prompt)
                    .GetMethods()
                    .Where(mi => mi.Name == nameof(Prompt.Select) && mi.GetParameters().Length == 5)
                    .Single()
                    .MakeGenericMethod(property.GetType());

                return (T)generic.Invoke(null, new object[] { message, property.GetType().GetEnumValues(), null, property, null });
            }
            else
            {
                var promptResult = Prompt.Input<T>(message, property, new[] { FieldValidation.ValidateProperty(model, memberName, property) });
                return promptResult is string str ? (T)(object)str.Trim() : promptResult;
            }
        }
    }
}
