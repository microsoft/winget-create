// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;
    using CommandLine;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCLI.Telemetry;
    using Microsoft.WingetCreateCLI.Telemetry.Events;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Common;
    using Microsoft.WingetCreateCore.Models;
    using Microsoft.WingetCreateCore.Models.DefaultLocale;
    using Microsoft.WingetCreateCore.Models.Installer;
    using Microsoft.WingetCreateCore.Models.Locale;
    using Microsoft.WingetCreateCore.Models.Singleton;
    using Microsoft.WingetCreateCore.Models.Version;
    using Octokit;
    using RestSharp;

    /// <summary>
    /// Abstract base command class that all commands inherit from.
    /// </summary>
    public abstract class BaseCommand
    {
        /// <summary>
        /// Default owner for winget-pkgs repository.
        /// </summary>
        public const string DefaultWingetRepoOwner = "microsoft";

        /// <summary>
        /// Default winget-pkgs repository.
        /// </summary>
        public const string DefaultWingetRepo = "winget-pkgs";

        /// <summary>
        /// Program name of the app.
        /// </summary>
        protected const string ProgramName = "wingetcreate";

        /// <summary>
        /// Program name of the app.
        /// </summary>
        protected const string ProgramApplicationAlias = "wingetcreate.exe";

        /// <summary>
        /// Gets or sets the GitHub token used to submit a pull request on behalf of the user.
        /// </summary>
        [Option('t', "token", Required = false, HelpText = "GitHubToken_HelpText", ResourceType = typeof(Resources))]
        public string GitHubToken { get; set; }

        /// <summary>
        /// Gets or sets the winget repo owner to use.
        /// </summary>
        public string WingetRepoOwner { get; set; } = DefaultWingetRepoOwner;

        /// <summary>
        /// Gets or sets the winget repo to use.
        /// </summary>
        public string WingetRepo { get; set; } = DefaultWingetRepo;

        /// <summary>
        /// Gets or sets the most recent pull request id associated with the command.
        /// </summary>
        public int PullRequestNumber { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not to submit the PR via a fork. Should be true when submitting as a user, false when submitting as an app.
        /// </summary>
        public bool SubmitPRToFork { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether or not to automatically open the PR webpage in the browser after creation.
        /// </summary>
        public bool OpenPRInBrowser { get; set; } = true;

        /// <summary>
        /// Gets the GitHubClient instance to use for interacting with GitHub from the CLI.
        /// </summary>
        protected GitHub GitHubClient { get; private set; }

        /// <summary>
        /// Validates the GitHubToken provided on the command-line, or if not present, the cached token if one exists.
        /// Attempts a simple operation against the target repo, and if that fails, then:
        ///     If token provided on command-line, errors out
        ///     If not, and cached token was present, then deletes token cache, and starts OAuth flow
        /// Otherwise, sets the instance variable to hold the validated token.
        /// If no token is present on command-line or in cache, starts the OAuth flow to retrieve one.
        /// </summary>
        /// <param name="cacheToken">Boolean to override default behavior and force caching of token.</param>
        /// <returns>True if the token is now present and valid, false otherwise.</returns>
        public async Task<bool> SetAndCheckGitHubToken(bool cacheToken = false)
        {
            string cachedToken = null;
            bool hasPatToken = !string.IsNullOrEmpty(this.GitHubToken);
            string token = this.GitHubToken;

            if (!hasPatToken)
            {
                Logger.Trace("No token parameter, reading cached token");
                token = cachedToken = GitHubOAuth.ReadTokenCache();

                if (string.IsNullOrEmpty(token))
                {
                    Logger.Trace("No cached token found.");
                    Logger.DebugLocalized(nameof(Resources.GitHubAccountMustBeLinked_Message));
                    Logger.DebugLocalized(nameof(Resources.ExecutionPaused_Message));
                    Console.WriteLine();
                    token = await GitHubOAuthLoginFlow();
                    if (string.IsNullOrEmpty(token))
                    {
                        // User must've cancelled OAuth flow, we can't proceed successfully
                        Logger.WarnLocalized(nameof(Resources.NoTokenResponse_Message));
                        return false;
                    }

                    Logger.DebugLocalized(nameof(Resources.ResumingCommandExecution_Message));
                }
                else
                {
                    Logger.DebugLocalized(nameof(Resources.UsingTokenFromCache_Message));
                }
            }

            this.GitHubClient = new GitHub(token, this.WingetRepoOwner, this.WingetRepo);

            try
            {
                Logger.Trace("Checking repo access using OAuth token");
                await this.GitHubClient.CheckAccess();
                Logger.Trace("Access check was successful, proceeding");
                this.GitHubToken = token;

                // Only cache the token if it came from Oauth, instead of PAT parameter or cache
                if (cacheToken || (!hasPatToken && token != cachedToken))
                {
                    try
                    {
                        Logger.Trace("Writing token to cache");
                        GitHubOAuth.WriteTokenCache(token);
                    }
                    catch (Exception ex)
                    {
                        // Failing to cache the token shouldn't be fatal.
                        Logger.WarnLocalized(nameof(Resources.WritingCacheTokenFailed_Message), ex.Message);
                    }
                }

                return true;
            }
            catch
            {
                if (token == cachedToken)
                {
                    // There's an issue with the cached token, so let's delete it and try again
                    Logger.WarnLocalized(nameof(Resources.InvalidCachedToken));
                    GitHubOAuth.DeleteTokenCache();
                    return await this.SetAndCheckGitHubToken();
                }
                else
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Abstract method executing the command.
        /// </summary>
        /// <returns>Boolean representing success or fail of the command.</returns>
        public abstract Task<bool> Execute();

        /// <summary>
        /// Creates a formatted directory of manifest files from the manifest object models and saves the directory to a local path.
        /// </summary>
        /// <param name="manifests">Wrapper object for manifest object models.</param>
        /// <param name="outputDir">Output directory where the manifests are saved locally.</param>
        /// <returns>Path to manifest directory.</returns>
        protected static string SaveManifestDirToLocalPath(
            Manifests manifests,
            string outputDir)
        {
            VersionManifest versionManifest = manifests.VersionManifest;
            InstallerManifest installerManifest = manifests.InstallerManifest;
            DefaultLocaleManifest defaultLocaleManifest = manifests.DefaultLocaleManifest;
            List<LocaleManifest> localeManifests = manifests.LocaleManifests;

            string defaultPackageLocale = defaultLocaleManifest.PackageLocale;
            string version = versionManifest.PackageVersion;
            string packageId = versionManifest.PackageIdentifier;
            string manifestDir = Utils.GetAppManifestDirPath(packageId, version);
            string fullDirPath = Path.Combine(outputDir, manifestDir);

            try
            {
                Directory.CreateDirectory(fullDirPath);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                string errorMessage = e.GetType().ToString() + ": " + e.Message;
                Logger.Error(errorMessage);
                outputDir = Directory.GetCurrentDirectory();
                Logger.WarnLocalized(nameof(Resources.ChangeOutputDirToCurrDir_Message), outputDir);
                fullDirPath = Path.Combine(outputDir, manifestDir);
            }

            string versionManifestFileName = $"{packageId}.yaml";
            string installerManifestFileName = $"{packageId}.installer.yaml";
            string defaultLocaleManifestFileName = $"{packageId}.locale.{defaultPackageLocale}.yaml";


            string producedBy = string.Join(" ", ProgramName, Utils.GetEntryAssemblyVersion());
            File.WriteAllText(Path.Combine(fullDirPath, versionManifestFileName), versionManifest.ToYaml(producedBy));
            File.WriteAllText(Path.Combine(fullDirPath, installerManifestFileName), installerManifest.ToYaml(producedBy));
            File.WriteAllText(Path.Combine(fullDirPath, defaultLocaleManifestFileName), defaultLocaleManifest.ToYaml(producedBy));

            foreach (LocaleManifest localeManifest in localeManifests)
            {
                string localeManifestFileName = $"{packageId}.locale.{localeManifest.PackageLocale}.yaml";
                File.WriteAllText(Path.Combine(fullDirPath, localeManifestFileName), localeManifest.ToYaml(producedBy));
            }

            Console.WriteLine();
            Logger.InfoLocalized(nameof(Resources.ManifestSaved_Message), fullDirPath);
            Console.WriteLine();

            return fullDirPath;
        }

        /// <summary>
        /// Launches the OAuth flow to allow the user to login to their GitHub account and grant permission to the app.
        /// </summary>
        /// <returns>Access token string.</returns>
        protected static async Task<string> GitHubOAuthLoginFlow()
        {
            Logger.DebugLocalized(nameof(Resources.InitiatingGitHubLogin_Message));
            Console.WriteLine();

            var client = new RestClient();
            var authorizationResponse = await GitHubOAuth.StartDeviceFlowAsync(client);

            Console.WriteLine(string.Format(Resources.LaunchBrowser_Message, authorizationResponse.VerificationUri));
            Logger.InfoLocalized(nameof(Resources.EnterUserCode_Message), authorizationResponse.UserCode);
            Console.WriteLine();
            GitHubOAuth.OpenWebPage(authorizationResponse.VerificationUri);
            var tokenResponse = await GitHubOAuth.GetTokenAsync(client, authorizationResponse);

            return tokenResponse?.AccessToken;
        }

        /// <summary>
        /// Downloads the package file from the provided installer url.
        /// </summary>
        /// /// <param name="installerUrl"> Installer Url to be downloaded. </param>
        /// <returns>Package file.</returns>
        protected static async Task<string> DownloadPackageFile(string installerUrl)
        {
            Logger.InfoLocalized(nameof(Resources.DownloadInstaller_Message), installerUrl);
            Console.WriteLine();

            try
            {
                string packageFile = await PackageParser.DownloadFileAsync(installerUrl);
                TelemetryManager.Log.WriteEvent(new DownloadInstallerEvent { IsSuccessful = true });
                return packageFile;
            }
            catch (Exception e)
            {
                TelemetryManager.Log.WriteEvent(new DownloadInstallerEvent
                {
                    IsSuccessful = false,
                    ExceptionType = e.GetType().ToString(),
                    StackTrace = e.StackTrace,
                    ErrorMessage = e.Message,
                });

                Logger.ErrorLocalized(nameof(Resources.DownloadFile_Error));

                if (e is HttpRequestException exception)
                {
                    Logger.ErrorLocalized(nameof(Resources.HttpResponseUnsuccessful_Error), exception.StatusCode);
                    return null;
                }

                if (e is InvalidOperationException)
                {
                    Logger.ErrorLocalized(nameof(Resources.InvalidUrl_Error));
                    return null;
                }

                throw;
            }
        }

        /// <summary>
        /// Utilizes WingetUtil to validate a specified manifest.
        /// </summary>
        /// <param name="manifestPath"> Path to the manifest file to be validated. </param>
        /// <returns>Bool indicating the validity of the manifest file. </returns>
        protected static bool ValidateManifest(string manifestPath)
        {
            (bool success, string message) = WinGetUtil.ValidateManifest(manifestPath);

            if (success)
            {
                Logger.InfoLocalized(nameof(Resources.ManifestValidationSucceeded_Message), success);
            }
            else
            {
                Logger.ErrorLocalized(nameof(Resources.ManifestValidationSucceeded_Message), success);
                Logger.Error(message);
                return false;
            }

            Console.WriteLine();
            return true;
        }

        /// <summary>
        /// Deserializes a list of manifest strings into their appropriate object models.
        /// </summary>
        /// <param name="manifestContents">List of manifest string contents.</param>
        /// <param name="manifests">Wrapper object for manifest object models.</param>
        protected static void DeserializeManifestContents(List<string> manifestContents, Manifests manifests)
        {
            foreach (string content in manifestContents)
            {
                ManifestTypeBase baseType = Serialization.DeserializeFromString<ManifestTypeBase>(content);

                if (baseType.ManifestType == "singleton")
                {
                    manifests.SingletonManifest = Serialization.DeserializeFromString<SingletonManifest>(content);
                }
                else if (baseType.ManifestType == "version")
                {
                    manifests.VersionManifest = Serialization.DeserializeFromString<VersionManifest>(content);
                }
                else if (baseType.ManifestType == "defaultLocale")
                {
                    manifests.DefaultLocaleManifest = Serialization.DeserializeFromString<DefaultLocaleManifest>(content);
                }
                else if (baseType.ManifestType == "locale")
                {
                    manifests.LocaleManifests.Add(Serialization.DeserializeFromString<LocaleManifest>(content));
                }
                else if (baseType.ManifestType == "installer")
                {
                    manifests.InstallerManifest = Serialization.DeserializeFromString<InstallerManifest>(content);
                }
            }
        }

        /// <summary>
        /// Prints out a preview of the manifest object models to console.
        /// </summary>
        /// <param name="manifests">Wrapper object containing the manifest object models.</param>
        protected static void DisplayManifestPreview(Manifests manifests)
        {
            Logger.Debug(Resources.GenerateManifestPreview_Message);
            Logger.Info(Resources.VersionManifestPreview_Message);
            string producedBy = string.Join(" ", ProgramName, Utils.GetEntryAssemblyVersion());
            Console.WriteLine(manifests.VersionManifest.ToYaml(producedBy));
            Logger.Info(Resources.InstallerManifestPreview_Message);
            Console.WriteLine(manifests.InstallerManifest.ToYaml(producedBy));
            Logger.Info(Resources.DefaultLocaleManifestPreview_Message);
            Console.WriteLine(manifests.DefaultLocaleManifest.ToYaml(producedBy));
        }

        /// <summary>
        /// Submits a pull request with multifile manifests using the user's GitHub access token.
        /// </summary>
        /// <param name="manifests">Wrapper object for manifest object models to be submitted.</param>
        /// <param name="token">Access token to allow for this tool to submit a pull request on behalf of the user.</param>
        /// <returns>A <see cref="Task"/> representing the success of the asynchronous operation.</returns>
        protected async Task<bool> GitHubSubmitManifests(Manifests manifests, string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                Logger.WarnLocalized(nameof(Resources.NoTokenProvided_Message));
                return false;
            }

            Logger.InfoLocalized(nameof(Resources.SubmittingPullRequest_Message));
            Console.WriteLine();

            try
            {
                string producedBy = string.Join(" ", ProgramName, Utils.GetEntryAssemblyVersion());
                PullRequest pullRequest = await this.GitHubClient.SubmitPullRequestAsync(manifests, producedBy, this.SubmitPRToFork);
                this.PullRequestNumber = pullRequest.Number;
                PullRequestEvent pullRequestEvent = new PullRequestEvent { IsSuccessful = true, PullRequestNumber = pullRequest.Number };
                TelemetryManager.Log.WriteEvent(pullRequestEvent);

                if (this.OpenPRInBrowser)
                {
                    Process.Start(new ProcessStartInfo(pullRequest.HtmlUrl) { UseShellExecute = true });
                }

                Logger.InfoLocalized(nameof(Resources.PullRequestURI_Message), pullRequest.HtmlUrl);
                Console.WriteLine();
            }
            catch (AggregateException ae)
            {
                ae.Handle((e) =>
                {
                    TelemetryManager.Log.WriteEvent(new PullRequestEvent
                    {
                        IsSuccessful = false,
                        ErrorMessage = e.Message,
                        ExceptionType = e.GetType().ToString(),
                        StackTrace = e.StackTrace,
                    });

                    if (e is Octokit.ForbiddenException)
                    {
                        Logger.ErrorLocalized(nameof(Resources.Error_Prefix), e.Message);
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                });
            }

            return true;
        }
    }
}
