// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using CommandLine;
    using CommandLine.Text;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Models.Settings;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCLI.Telemetry;
    using Microsoft.WingetCreateCLI.Telemetry.Events;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Common;
    using Microsoft.WingetCreateCore.Models;
    using Microsoft.WingetCreateCore.Models.Locale;
    using Newtonsoft.Json;
    using Sharprompt;

    /// <summary>
    /// Command to create a new locale manifest for an existing manifest in the Windows Package Manager repository.
    /// </summary>
    [Verb("new-locale", HelpText = "NewLocaleCommand_HelpText", ResourceType = typeof(Resources))]
    public class NewLocaleCommand : BaseCommand
    {
        private readonly List<string> defaultPromptPropertiesForNewLocale = new()
        {
            nameof(LocaleManifest.PackageLocale),
            nameof(LocaleManifest.PackageName),
            nameof(LocaleManifest.Publisher),
            nameof(LocaleManifest.License),
            nameof(LocaleManifest.ShortDescription),
        };

        private bool localeArgumentUsed = false;

        /// <summary>
        /// Gets the usage examples for the new-locale command.
        /// </summary>
        [Usage(ApplicationAlias = ProgramApplicationAlias)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example(Resources.Example_NewLocaleCommand_AddForLatestVersion, new NewLocaleCommand { Id = "<PackageIdentifier>", GitHubToken = "<GitHubPersonalAccessToken>" });
                yield return new Example(Resources.Example_NewLocaleCommand_AddForSpecificVersion, new NewLocaleCommand { Id = "<PackageIdentifier>", Version = "<Version>", GitHubToken = "<GitHubPersonalAccessToken>" });
                yield return new Example(Resources.Example_NewLocaleCommand_SaveToDirectory, new NewLocaleCommand { Id = "<PackageIdentifier>", Locale = "<Locale>", Version = "<Version>", OutputDir = "<OutputDirectory>", GitHubToken = "<GitHubPersonalAccessToken>" });
            }
        }

        /// <summary>
        /// Gets or sets the id used for looking up an existing manifest in the Windows Package Manager repository.
        /// </summary>
        [Value(0, MetaName = "PackageIdentifier", Required = true, HelpText = "PackageIdentifier_HelpText", ResourceType = typeof(Resources))]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the version of the package from the Windows Package Manager repository to create the new locales for.
        /// </summary>
        [Option('v', "version", Required = false, Default = null, HelpText = "NewLocaleCommand_Version_HelpText", ResourceType = typeof(Resources))]
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the locale to create a new manifest for.
        /// </summary>
        [Option('l', "locale", Required = false, HelpText = "NewLocaleCommand_Locale_HelpText", ResourceType = typeof(Resources))]
        public string Locale { get; set; }

        /// <summary>
        /// Gets or sets the reference locale to use for generating the new locale manifest.
        /// </summary>
        [Option('r', "reference-locale", Required = false, HelpText = "ReferenceLocale_HelpText", ResourceType = typeof(Resources))]
        public string ReferenceLocale { get; set; }

        /// <summary>
        /// Gets or sets the outputPath where the generated manifest file should be saved to.
        /// </summary>
        [Option('o', "out", Required = false, HelpText = "OutputDirectory_HelpText", ResourceType = typeof(Resources))]
        public string OutputDir { get; set; }

        /// <summary>
        /// Gets or sets the format of the output manifest files.
        /// </summary>
        [Option('f', "format", Required = false, HelpText = "ManifestFormat_HelpText", ResourceType = typeof(Resources))]
        public override ManifestFormat Format { get => base.Format; set => base.Format = value; }

        /// <summary>
        /// Gets or sets the GitHub token used to submit a pull request on behalf of the user.
        /// </summary>
        [Option('t', "token", Required = false, HelpText = "GitHubToken_HelpText", ResourceType = typeof(Resources))]
        public override string GitHubToken { get => base.GitHubToken; set => base.GitHubToken = value; }

        /// <summary>
        /// Gets or sets a value indicating whether the PR should be opened automatically in the browser.
        /// </summary>
        [Option('n', "no-open", Required = false, HelpText = "NoOpenPRInBrowser_HelpText", ResourceType = typeof(Resources))]
        public bool NoOpenPRInBrowser { get => !this.OpenPRInBrowser; set => this.OpenPRInBrowser = !value; }

        /// <summary>
        /// Executes the new-locale command flow.
        /// </summary>
        /// <returns>Boolean representing success or fail of the command.</returns>
        public override async Task<bool> Execute()
        {
            CommandExecutedEvent commandEvent = new CommandExecutedEvent
            {
                Command = nameof(NewLocaleCommand),
                Id = this.Id,
                Version = this.Version,
                HasGitHubToken = !string.IsNullOrEmpty(this.GitHubToken),
            };

            try
            {
                Prompt.Symbols.Done = new Symbol(string.Empty, string.Empty);
                Prompt.Symbols.Prompt = new Symbol(string.Empty, string.Empty);

                // Validate format of input locale and reference locale arguments
                try
                {
                    if (!string.IsNullOrEmpty(this.Locale))
                    {
                        _ = new RegionInfo(this.Locale);
                    }

                    if (!string.IsNullOrEmpty(this.ReferenceLocale))
                    {
                        _ = new RegionInfo(this.ReferenceLocale);
                    }
                }
                catch (ArgumentException)
                {
                    Logger.ErrorLocalized(nameof(Resources.InvalidLocale_ErrorMessage));
                    return false;
                }

                string exactId;
                try
                {
                    exactId = await this.GitHubClient.FindPackageId(this.Id);
                }
                catch (Octokit.RateLimitExceededException)
                {
                    Logger.ErrorLocalized(nameof(Resources.RateLimitExceeded_Message));
                    return false;
                }

                if (!string.IsNullOrEmpty(exactId))
                {
                    this.Id = exactId;
                }

                List<string> manifestContent;

                try
                {
                    manifestContent = await this.GitHubClient.GetManifestContentAsync(this.Id, this.Version);
                }
                catch (Octokit.NotFoundException e)
                {
                    Logger.ErrorLocalized(nameof(Resources.Error_Prefix), e.Message);
                    Logger.ErrorLocalized(nameof(Resources.OctokitNotFound_Error));
                    return false;
                }

                Manifests originalManifests = Serialization.DeserializeManifestContents(manifestContent);

                if (!string.IsNullOrEmpty(this.Locale))
                {
                    try
                    {
                        if (LocaleHelper.DoesLocaleManifestExist(this.Locale, originalManifests))
                        {
                            Logger.ErrorLocalized(nameof(Resources.LocaleAlreadyExists_ErrorMessage), this.Locale);
                            return false;
                        }
                    }
                    catch (ArgumentException)
                    {
                        Logger.ErrorLocalized(nameof(Resources.InvalidLocale_ErrorMessage));
                        return false;
                    }
                }

                // Validate input reference locale argument and set the reference locale
                LocaleManifest referenceLocaleManifest;

                if (!string.IsNullOrEmpty(this.ReferenceLocale))
                {
                    try
                    {
                        referenceLocaleManifest = (LocaleManifest)LocaleHelper.GetMatchingLocaleManifest(this.ReferenceLocale, originalManifests);
                    }
                    catch (ArgumentException)
                    {
                        Logger.ErrorLocalized(nameof(Resources.InvalidLocale_ErrorMessage));
                        return false;
                    }

                    if (referenceLocaleManifest == null)
                    {
                        Logger.ErrorLocalized(nameof(Resources.ReferenceLocaleNotFound_Error));
                        return false;
                    }
                }
                else
                {
                    referenceLocaleManifest = originalManifests.DefaultLocaleManifest.ToLocaleManifest();
                }

                Console.WriteLine(Resources.NewLocaleCommand_Header);
                Console.WriteLine();
                Logger.InfoLocalized(nameof(Resources.ManifestDocumentation_HelpText), Constants.ManifestDocumentationUrl);
                Console.WriteLine();
                Console.WriteLine(Resources.NewLocaleCommand_PrePrompt_Header);
                Console.WriteLine();

                Logger.DebugLocalized(nameof(Resources.EnterFollowingFields_Message));

                List<LocaleManifest> newLocales = new();
                do
                {
                    LocaleManifest newlocale = this.GenerateLocaleManifest(originalManifests, referenceLocaleManifest);
                    Console.WriteLine();
                    ValidateManifestsInTempDir(originalManifests, this.Format);
                    originalManifests.LocaleManifests.Add(newlocale);
                    newLocales.Add(newlocale);
                }
                while (Prompt.Confirm(Resources.CreateAnotherLocale_Message));

                Console.WriteLine();
                EnsureManifestVersionConsistency(originalManifests);
                this.DisplayGeneratedLocales(newLocales);

                if (string.IsNullOrEmpty(this.OutputDir))
                {
                    this.OutputDir = Directory.GetCurrentDirectory();
                }

                string manifestDirectoryPath = SaveManifestDirToLocalPath(originalManifests, this.OutputDir);

                if (ValidateManifest(manifestDirectoryPath, this.Format))
                {
                    if (Prompt.Confirm(Resources.ConfirmGitHubSubmitManifest_Message))
                    {
                        return await this.LoadGitHubClient(true) ?
                            (commandEvent.IsSuccessful = await this.GitHubSubmitManifests(
                                originalManifests,
                                $"Add locale: {originalManifests.VersionManifest.PackageIdentifier} version {originalManifests.VersionManifest.PackageVersion}"))
                            : false;
                    }
                    else
                    {
                        Logger.WarnLocalized(nameof(Resources.SkippingPullRequest_Message));
                    }
                }
                else
                {
                    return false;
                }

                return await Task.FromResult(commandEvent.IsSuccessful = true);
            }
            finally
            {
                TelemetryManager.Log.WriteEvent(commandEvent);
            }
        }

        private void PopulateLocalePropertiesWithDefaultValues(LocaleManifest reference, LocaleManifest target, List<string> properties)
        {
            foreach (string propertyName in properties)
            {
                if (propertyName == nameof(LocaleManifest.PackageLocale))
                {
                    continue;
                }

                var value = reference.GetType().GetProperty(propertyName).GetValue(reference);
                if (value != null)
                {
                    target.GetType().GetProperty(propertyName).SetValue(target, value);
                }
            }
        }

        private LocaleManifest GenerateLocaleManifest(Manifests originalManifests, LocaleManifest referenceLocaleManifest)
        {
            LocaleManifest newLocaleManifest = new LocaleManifest
            {
                PackageIdentifier = originalManifests.VersionManifest.PackageIdentifier,
                PackageVersion = originalManifests.VersionManifest.PackageVersion,
            };

            // Fill in properties from the reference locale. This is to help user see previous values to quickly fill out the new manifest.
            this.PopulateLocalePropertiesWithDefaultValues(referenceLocaleManifest, newLocaleManifest, this.defaultPromptPropertiesForNewLocale);

            if (!string.IsNullOrEmpty(this.Locale) && !this.localeArgumentUsed)
            {
                newLocaleManifest.PackageLocale = this.Locale;

                // Don't prompt for PackageLocale if it is provided as an argument.
                List<string> properties = new(this.defaultPromptPropertiesForNewLocale);
                properties.Remove(nameof(LocaleManifest.PackageLocale));

                Logger.DebugLocalized(nameof(Resources.FieldSetToValue_Message), nameof(LocaleManifest.PackageLocale), this.Locale);
                LocaleHelper.PromptAndSetLocaleProperties(newLocaleManifest, properties, originalManifests);
                this.localeArgumentUsed = true;
            }
            else
            {
                LocaleHelper.PromptAndSetLocaleProperties(newLocaleManifest, this.defaultPromptPropertiesForNewLocale, originalManifests);
            }

            Console.WriteLine();
            if (Prompt.Confirm(Resources.AddAdditionalLocaleProperties_Message))
            {
                // Get optional properties that have not been prompted before.
                List<string> optionalProperties = newLocaleManifest.GetType().GetProperties().ToList().Where(p =>
                                p.GetCustomAttribute<RequiredAttribute>() == null &&
                                p.GetCustomAttribute<JsonPropertyAttribute>() != null &&
                                !this.defaultPromptPropertiesForNewLocale.Any(d => d == p.Name))
                                .Select(p => p.Name).ToList();
                this.PopulateLocalePropertiesWithDefaultValues(referenceLocaleManifest, newLocaleManifest, optionalProperties);
                PromptOptionalProperties(newLocaleManifest, optionalProperties);
            }

            return newLocaleManifest;
        }

        private void DisplayGeneratedLocales(List<LocaleManifest> newLocales)
        {
            Logger.DebugLocalized(nameof(Resources.GenerateNewLocalePreview_Message));
            foreach (var localeManifest in newLocales)
            {
                Logger.InfoLocalized(nameof(Resources.LocaleManifest_Message), localeManifest.PackageLocale);
                Console.WriteLine(localeManifest.ToManifestString(true));
            }
        }
    }
}
