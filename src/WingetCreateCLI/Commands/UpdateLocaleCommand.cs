// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using CommandLine;
    using CommandLine.Text;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Models.Settings;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCLI.Telemetry;
    using Microsoft.WingetCreateCLI.Telemetry.Events;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Models;
    using Microsoft.WingetCreateCore.Models.DefaultLocale;
    using Microsoft.WingetCreateCore.Models.Locale;
    using Sharprompt;

    /// <summary>
    /// Command to update an existing locale manifest for a package in the Windows Package Manager repository.
    /// </summary>
    [Verb("update-locale", HelpText = "UpdateLocaleCommand_HelpText", ResourceType = typeof(Resources))]
    public class UpdateLocaleCommand : BaseCommand
    {
        /// <summary>
        /// Gets the usage examples for the update-locale command.
        /// </summary>
        [Usage(ApplicationAlias = ProgramApplicationAlias)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example(Resources.Example_UpdateLocaleCommand_UpdateForLatestVersion, new UpdateLocaleCommand { Id = "<PackageIdentifier>", GitHubToken = "<GitHubPersonalAccessToken>" });
                yield return new Example(Resources.Example_UpdateLocaleCommand_UpdateForSpecificVersion, new UpdateLocaleCommand { Id = "<PackageIdentifier>", Version = "<Version>", GitHubToken = "<GitHubPersonalAccessToken>" });
                yield return new Example(Resources.Example_UpdateLocaleCommand_SaveToDirectory, new UpdateLocaleCommand { Id = "<PackageIdentifier>", Locale = "<Locale>", Version = "<Version>", OutputDir = "<OutputDirectory>", GitHubToken = "<GitHubPersonalAccessToken>" });
            }
        }

        /// <summary>
        /// Gets or sets the id used for looking up an existing manifest in the Windows Package Manager repository.
        /// </summary>
        [Value(0, MetaName = "PackageIdentifier", Required = true, HelpText = "PackageIdentifier_HelpText", ResourceType = typeof(Resources))]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the version of the package from the Windows Package Manager repository to update the locales for.
        /// </summary>
        [Option('v', "version", Required = false, Default = null, HelpText = "UpdateLocaleCommand_Version_HelpText", ResourceType = typeof(Resources))]
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the locale to update the manifest for.
        /// </summary>
        [Option('l', "locale", Required = false, HelpText = "UpdateLocaleCommand_Locale_HelpText", ResourceType = typeof(Resources))]
        public string Locale { get; set; }

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
        /// Executes the update-locale command flow.
        /// </summary>
        /// <returns>Boolean representing success or fail of the command.</returns>
        public override async Task<bool> Execute()
        {
            CommandExecutedEvent commandEvent = new CommandExecutedEvent
            {
                Command = nameof(UpdateLocaleCommand),
                Id = this.Id,
                Version = this.Version,
                HasGitHubToken = !string.IsNullOrEmpty(this.GitHubToken),
            };

            try
            {
                Prompt.Symbols.Done = new Symbol(string.Empty, string.Empty);
                Prompt.Symbols.Prompt = new Symbol(string.Empty, string.Empty);

                // Validate format of input locale argument
                try
                {
                    if (!string.IsNullOrEmpty(this.Locale))
                    {
                        _ = new RegionInfo(this.Locale);
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

                // Validate input locale argument
                if (!string.IsNullOrEmpty(this.Locale))
                {
                    try
                    {
                        if (!LocaleHelper.DoesLocaleManifestExist(this.Locale, originalManifests))
                        {
                            Logger.ErrorLocalized(nameof(Resources.LocaleDoesNotExist_Message), this.Locale);
                            return false;
                        }
                    }
                    catch (ArgumentException)
                    {
                        Logger.ErrorLocalized(nameof(Resources.InvalidLocale_ErrorMessage));
                        return false;
                    }
                }

                var updatedLocalesTuple = this.PromptAndUpdateExistingLocales(originalManifests);
                Console.WriteLine();
                EnsureManifestVersionConsistency(originalManifests);
                this.DisplayUpdatedLocales(updatedLocalesTuple);

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
                                $"Update locale: {originalManifests.VersionManifest.PackageIdentifier} version {originalManifests.VersionManifest.PackageVersion}"))
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

        private object PromptAllLocalesAsMenuSelection(DefaultLocaleManifest defaultLocale, List<LocaleManifest> localeManifests)
        {
            Dictionary<string, object> localeMap = new Dictionary<string, object>
            {
                { string.Format("{0} ({1})", defaultLocale.PackageLocale, Resources.DefaultLocale_MenuItem), defaultLocale },
            };

            foreach (var locale in localeManifests)
            {
                localeMap.Add(locale.PackageLocale, locale);
            }

            string selectedLocale = Prompt.Select(Resources.SelectExistingLocale_Message, localeMap.Keys.ToList(), defaultValue: localeMap.Keys.First());
            return localeMap[selectedLocale];
        }

        private void DisplayUpdatedLocales(Tuple<DefaultLocaleManifest, List<LocaleManifest>> updatedLocales)
        {
            Logger.DebugLocalized(nameof(Resources.GenerateUpdatedLocalePreview_Message));
            if (updatedLocales.Item1 != null)
            {
                DisplayDefaultLocaleManifest(updatedLocales.Item1);
            }

            if (updatedLocales.Item2 != null)
            {
                DisplayLocaleManifests(updatedLocales.Item2);
            }
        }

        /// <summary>
        /// Displays a list of existing locale manifests and prompts user to update properties for the selected locale.
        /// </summary>
        /// <param name="manifests">Object model containing existing locale manifests.</param>
        /// <returns>A tuple containing the updated locale manifests.</returns>
        private Tuple<DefaultLocaleManifest, List<LocaleManifest>> PromptAndUpdateExistingLocales(Manifests manifests)
        {
            // Object containing only the locales that are updated by the user.
            Manifests updatedLocales = new Manifests();

            bool localeArgumentUsed = false;
            object selectedLocale;

            // Properties that should not be prompted for.
            List<string> excludedProperties = new List<string>()
            {
                nameof(LocaleManifest.PackageIdentifier),
                nameof(LocaleManifest.PackageVersion),
                nameof(LocaleManifest.PackageLocale),
                nameof(LocaleManifest.ManifestType),
                nameof(LocaleManifest.ManifestVersion),
            };

            do
            {
                if (!string.IsNullOrEmpty(this.Locale) && !localeArgumentUsed)
                {
                    selectedLocale = LocaleHelper.GetMatchingLocaleManifest(this.Locale, manifests);
                    localeArgumentUsed = true;
                }
                else
                {
                    selectedLocale = this.PromptAllLocalesAsMenuSelection(manifests.DefaultLocaleManifest, manifests.LocaleManifests);
                }

                if (selectedLocale is DefaultLocaleManifest defaultLocaleManifest)
                {
                    Logger.DebugLocalized(nameof(Resources.FieldSetToValue_Message), nameof(LocaleManifest.PackageLocale), defaultLocaleManifest.PackageLocale);
                    var properties = LocaleHelper.GetLocalePropertyNames(defaultLocaleManifest).Except(excludedProperties).ToList();
                    LocaleHelper.PromptAndSetLocaleProperties(defaultLocaleManifest, properties);
                    updatedLocales.DefaultLocaleManifest = defaultLocaleManifest;
                }
                else if (selectedLocale is LocaleManifest localeManifest)
                {
                    Logger.DebugLocalized(nameof(Resources.FieldSetToValue_Message), nameof(LocaleManifest.PackageLocale), localeManifest.PackageLocale);
                    var properties = LocaleHelper.GetLocalePropertyNames(localeManifest).Except(excludedProperties).ToList();
                    LocaleHelper.PromptAndSetLocaleProperties(localeManifest, properties);
                    updatedLocales.LocaleManifests.Add(localeManifest);
                }

                Console.WriteLine();
                ValidateManifestsInTempDir(manifests, this.Format);
            }
            while (Prompt.Confirm(Resources.UpdateAnotherLocale_Message));

            return new Tuple<DefaultLocaleManifest, List<LocaleManifest>>(updatedLocales.DefaultLocaleManifest, updatedLocales.LocaleManifests);
        }
    }
}
