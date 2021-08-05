// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
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
        protected const string ProgramApplicationAlias = "wingetcreate.exe";

        /// <summary>
        /// Gets or sets the GitHub token used to submit a pull request on behalf of the user.
        /// </summary>
        public virtual string GitHubToken { get; set; }

        /// <summary>
        /// Gets or sets the winget repo owner to use.
        /// </summary>
        public string WingetRepoOwner { get; set; } = UserSettings.WindowsPackageManagerRepositoryOwner ?? DefaultWingetRepoOwner;

        /// <summary>
        /// Gets or sets the winget repo to use.
        /// </summary>
        public string WingetRepo { get; set; } = UserSettings.WindowsPackageManagerRepositoryName ?? DefaultWingetRepo;

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
        internal GitHub GitHubClient { get; private set; }

        /// <summary>
        /// Abstract method executing the command.
        /// </summary>
        /// <returns>Boolean representing success or fail of the command.</returns>
        public abstract Task<bool> Execute();

        /// <summary>
        /// Creates a new GitHub client using the provided or cached token if present.
        /// If the requireToken bool is set to TRUE, OAuth flow can be launched to acquire a new token for the client.
        /// The OAuth flow will only be launched if no token is provided in the command line and no token is present in the token cache.
        /// </summary>
        /// <param name="requireToken">Boolean value indicating whether a token is required for the client and whether to initiate an OAuth flow.</param>
        /// <returns>A boolean value indicating whether a new GitHub client was created and accessed successfully.</returns>
        public async Task<bool> LoadGitHubClient(bool requireToken = false)
        {
            bool isCacheToken = false;

            if (string.IsNullOrEmpty(this.GitHubToken))
            {
                Logger.Trace("No token parameter, reading cached token");
                this.GitHubToken = GitHubOAuth.ReadTokenCache();

                if (string.IsNullOrEmpty(this.GitHubToken))
                {
                    if (requireToken)
                    {
                        Logger.Trace("No token found in cache, launching OAuth flow");
                        if (!await this.GetTokenFromOAuth())
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    isCacheToken = true;
                }
            }

            if (await this.CheckGitHubTokenAndSetClient())
            {
                return true;
            }
            else
            {
                if (isCacheToken)
                {
                    GitHubOAuth.DeleteTokenCache();
                }

                return false;
            }
        }

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

            File.WriteAllText(Path.Combine(fullDirPath, versionManifestFileName), versionManifest.ToYaml());
            File.WriteAllText(Path.Combine(fullDirPath, installerManifestFileName), installerManifest.ToYaml());
            File.WriteAllText(Path.Combine(fullDirPath, defaultLocaleManifestFileName), defaultLocaleManifest.ToYaml());

            foreach (LocaleManifest localeManifest in localeManifests)
            {
                string localeManifestFileName = $"{packageId}.locale.{localeManifest.PackageLocale}.yaml";
                File.WriteAllText(Path.Combine(fullDirPath, localeManifestFileName), localeManifest.ToYaml());
            }

            Console.WriteLine();
            Logger.InfoLocalized(nameof(Resources.ManifestSaved_Message), fullDirPath);
            Console.WriteLine();

            return fullDirPath;
        }

        /// <summary>
        /// Saves the manifests to a randomly generated directory in the %TEMP% folder and validates them, printing the results to console.
        /// </summary>
        /// <param name="manifests">Manifests object model.</param>
        /// <returns>A boolean value indicating whether validation of the manifests was successful.</returns>
        protected static bool ValidateManifestsInTempDir(Manifests manifests)
        {
            string packageId = manifests.VersionManifest.PackageIdentifier;
            string defaultPackageLocale = manifests.DefaultLocaleManifest.PackageLocale;
            string versionManifestFileName = $"{packageId}.yaml";
            string installerManifestFileName = $"{packageId}.installer.yaml";
            string defaultLocaleManifestFileName = $"{packageId}.locale.{defaultPackageLocale}.yaml";

            string randomDirPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(randomDirPath);

            File.WriteAllText(Path.Combine(randomDirPath, versionManifestFileName), manifests.VersionManifest.ToYaml());
            File.WriteAllText(Path.Combine(randomDirPath, installerManifestFileName), manifests.InstallerManifest.ToYaml());
            File.WriteAllText(Path.Combine(randomDirPath, defaultLocaleManifestFileName), manifests.DefaultLocaleManifest.ToYaml());

            bool result = ValidateManifest(randomDirPath);
            Directory.Delete(randomDirPath, true);
            return result;
        }

        /// <summary>
        /// Displays the appropriate warning messages for installers with detected architecture mismatches.
        /// </summary>
        /// <param name="detectedArchs">List of DetectedArch objects that represent each installers detected architectures.</param>
        protected static void DisplayMismatchedArchitectures(List<PackageParser.DetectedArch> detectedArchs)
        {
            var mismatchedArchInstallers = detectedArchs.Where(i => i.UrlArch.HasValue && i.UrlArch != i.BinaryArch);

            if (mismatchedArchInstallers.Any())
            {
                Logger.WarnLocalized(nameof(Resources.DetectedArchMismatch_Message));
                Console.WriteLine();
                foreach (var installer in mismatchedArchInstallers)
                {
                    Logger.WarnLocalized(nameof(Resources.InstallerBinaryMismatch_Message), installer.UrlArch, installer.BinaryArch);
                    Logger.Warn($"{installer.Url}");
                    Console.WriteLine();
                }
            }
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
        /// Download all packages specified by urls, and return list of downloaded file paths.
        /// </summary>
        /// <param name="urls">Urls for packages to download.</param>
        /// <returns>List of file paths downloaded.</returns>
        protected static async Task<IList<string>> DownloadInstallers(IEnumerable<string> urls)
        {
            var packageFiles = new List<string>();

            foreach (var url in urls)
            {
                string packageFile = await DownloadPackageFile(url);
                if (string.IsNullOrEmpty(packageFile))
                {
                    return null;
                }

                packageFiles.Add(packageFile);
            }

            return packageFiles;
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
        /// Prints out a preview of the manifest object models to console.
        /// </summary>
        /// <param name="manifests">Wrapper object containing the manifest object models.</param>
        protected static void DisplayManifestPreview(Manifests manifests)
        {
            Logger.Debug(Resources.GenerateManifestPreview_Message);
            Logger.Info(Resources.VersionManifestPreview_Message);
            Console.WriteLine(manifests.VersionManifest.ToYaml());
            Logger.Info(Resources.InstallerManifestPreview_Message);
            Console.WriteLine(manifests.InstallerManifest.ToYaml());
            Logger.Info(Resources.DefaultLocaleManifestPreview_Message);
            Console.WriteLine(manifests.DefaultLocaleManifest.ToYaml());
        }

        /// <summary>
        /// Launches the GitHub OAuth flow and obtains a GitHub token.
        /// </summary>
        /// <returns>A boolean value indicating whether the OAuth login flow was successful.</returns>
        protected async Task<bool> GetTokenFromOAuth()
        {
            Logger.DebugLocalized(nameof(Resources.GitHubAccountMustBeLinked_Message));
            Logger.DebugLocalized(nameof(Resources.ExecutionPaused_Message));
            Console.WriteLine();
            this.GitHubToken = await GitHubOAuthLoginFlow();

            if (string.IsNullOrEmpty(this.GitHubToken))
            {
                // User must've cancelled OAuth flow, we can't proceed successfully
                Logger.WarnLocalized(nameof(Resources.NoTokenResponse_Message));
                return false;
            }

            this.StoreTokenInCache();
            Logger.DebugLocalized(nameof(Resources.ResumingCommandExecution_Message));
            return true;
        }

        /// <summary>
        /// If the provided token is valid, stores the token in cache.
        /// </summary>
        /// <returns>Returns a boolean value indicating whether storing the token in cache was successful.</returns>
        protected bool StoreTokenInCache()
        {
            try
            {
                Logger.Trace("Writing token to cache");
                GitHubOAuth.WriteTokenCache(this.GitHubToken);
                Logger.InfoLocalized(nameof(Resources.StoringToken_Message));
            }
            catch (Exception ex)
            {
                // Failing to cache the token shouldn't be fatal.
                Logger.WarnLocalized(nameof(Resources.WritingCacheTokenFailed_Message), ex.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Verifies if the GitHub token has valid access.
        /// </summary>
        /// <returns>A boolean value indicating whether the GitHub token had valid access.</returns>
        protected async Task<bool> CheckGitHubTokenAndSetClient()
        {
            var client = new GitHub(this.GitHubToken, this.WingetRepoOwner, this.WingetRepo);

            try
            {
                Logger.Trace("Checking repo access using provided token");
                await client.CheckAccess();
                Logger.Trace("Access check was successful, proceeding");
            }
            catch (Exception e)
            {
                if (e is AuthorizationException)
                {
                    Logger.ErrorLocalized(nameof(Resources.InvalidGitHubToken_Message));
                }
                else if (e is RateLimitExceededException)
                {
                    Logger.ErrorLocalized(nameof(Resources.RateLimitExceeded_Message));
                }
                else if (e is NotFoundException)
                {
                    Logger.ErrorLocalized(nameof(Resources.RepositoryNotFound_Error), this.WingetRepoOwner, this.WingetRepo);
                }

                return false;
            }

            this.GitHubClient = client;
            return true;
        }

        /// <summary>
        /// Submits a pull request with multifile manifests using the user's GitHub access token.
        /// </summary>
        /// <param name="manifests">Wrapper object for manifest object models to be submitted.</param>
        /// <returns>A <see cref="Task"/> representing the success of the asynchronous operation.</returns>
        protected async Task<bool> GitHubSubmitManifests(Manifests manifests)
        {
            if (string.IsNullOrEmpty(this.GitHubToken))
            {
                Logger.WarnLocalized(nameof(Resources.NoTokenProvided_Message));
                return false;
            }

            Logger.InfoLocalized(nameof(Resources.SubmittingPullRequest_Message));
            Console.WriteLine();

            try
            {
                PullRequest pullRequest = await this.GitHubClient.SubmitPullRequestAsync(manifests, this.SubmitPRToFork);
                this.PullRequestNumber = pullRequest.Number;
                PullRequestEvent pullRequestEvent = new PullRequestEvent { IsSuccessful = true, PullRequestNumber = pullRequest.Number };
                TelemetryManager.Log.WriteEvent(pullRequestEvent);

                if (this.OpenPRInBrowser)
                {
                    GitHubOAuth.OpenWebPage(pullRequest.HtmlUrl);
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
