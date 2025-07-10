// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using CommandLine;
    using CommandLine.Text;
    using Microsoft.WingetCreateCLI.Commands;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCLI.Telemetry;
    using Microsoft.WingetCreateCLI.Telemetry.Events;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Common;

    /// <summary>
    /// Main entry class for the CLI.
    /// </summary>
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            Logger.Initialize();
            TelemetryEventListener.EventListener.IsTelemetryEnabled();
            SentenceBuilder.Factory = () => new LocalizableSentenceBuilder();
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            string arguments = string.Join(' ', Environment.GetCommandLineArgs());
            Logger.Trace($"Command line args: {arguments}");

            Parser myParser = new Parser(config =>
            {
                config.HelpWriter = null;
                config.CaseSensitive = false;
                config.CaseInsensitiveEnumValues = true;
            });

            var types = new Type[]
            {
                typeof(NewCommand),
                typeof(UpdateCommand),
                typeof(NewLocaleCommand),
                typeof(UpdateLocaleCommand),
                typeof(SubmitCommand),
                typeof(SettingsCommand),
                typeof(TokenCommand),
                typeof(CacheCommand),
                typeof(ShowCommand),
                typeof(InfoCommand),
                typeof(DscCommand),
            };
            var parserResult = myParser.ParseArguments(args, types);

            BaseCommand command = parserResult.MapResult(c => c as BaseCommand, err => null);
            if (command == null)
            {
                DisplayHelp(parserResult as NotParsed<object>);
                DisplayParsingErrors(parserResult as NotParsed<object>);
                return args.Any() ? 1 : 0;
            }

            if (command is not DscCommand)
            {
                // For DSC commands, we do not want to display the header to
                // ensure the output is a valid JSON.
                UserSettings.FirstRunTelemetryConsent();
            }

            // If the user has provided a token via the command line, warn them that it may be logged
            if (!string.IsNullOrEmpty(command.GitHubToken))
            {
                Logger.WarnLocalized(nameof(Resources.GitHubTokenWarning_Message));
            }

            // Do not load github client for commands that do not deal with a GitHub token.
            if (command.AcceptsGitHubToken)
            {
                if (await command.LoadGitHubClient())
                {
                    try
                    {
                        string latestVersion = await command.GitHubClient.GetLatestRelease();
                        string trimmedVersion = latestVersion.TrimStart('v').Split('-').First();
                        if (trimmedVersion != Utils.GetEntryAssemblyVersion())
                        {
                            Logger.WarnLocalized(nameof(Resources.OutdatedVersionNotice_Message));
                            Logger.WarnLocalized(nameof(Resources.GetLatestVersion_Message), latestVersion, Constants.GitHubReleasesUrl);
                            Logger.WarnLocalized(nameof(Resources.UpgradeUsingWinget_Message));
                            Console.WriteLine();
                        }
                    }
                    catch (Exception ex) when (ex is Octokit.ApiException || ex is Octokit.RateLimitExceededException)
                    {
                        // Since this is only notifying the user if an update is available, don't block if the token is invalid or a rate limit error is encountered.
                    }
                }
                else
                {
                    // Do not block creating a new manifest if loading the GitHub client fails. InstallerURL could point to a local network.
                    if (command is not NewCommand)
                    {
                        return 1;
                    }
                }
            }

            try
            {
                Serialization.SetManifestSerializer(command.Format.ToString());
            }
            catch (ArgumentException ex)
            {
                Logger.ErrorLocalized(nameof(Resources.InvalidManifestFormat_ErrorMessage));
                TelemetryManager.Log.WriteEvent(new GlobalExceptionEvent
                {
                    ExceptionType = ex.GetType().ToString(),
                    ErrorMessage = ex.Message,
                    StackTrace = ex.StackTrace,
                });
                return 1;
            }

            try
            {
                WingetCreateCore.Serialization.ProducedBy = string.Join(" ", Constants.ProgramName, Utils.GetEntryAssemblyVersion());
                return await command.Execute() ? 0 : 1;
            }
            catch (ArgumentException)
            {
                Logger.ErrorLocalized(nameof(Resources.InvalidManifestFormat_ErrorMessage));
                return 1;
            }
            catch (Exception ex)
            {
                TelemetryManager.Log.WriteEvent(new GlobalExceptionEvent
                {
                    ExceptionType = ex.GetType().ToString(),
                    ErrorMessage = ex.Message,
                    StackTrace = ex.StackTrace,
                });

                Logger.Error(ex.ToString());
                return 1;
            }
            finally
            {
                if (!UserSettings.CleanUpDisabled)
                {
                    Common.CleanUpFilesOlderThan(PackageParser.InstallerDownloadPath, UserSettings.CleanUpDays);
                    Common.CleanUpFilesOlderThan(Path.Combine(Common.LocalAppStatePath, Constants.DiagnosticOutputDirectoryFolderName), UserSettings.CleanUpDays);
                }
            }
        }

        private static void DisplayHelp(NotParsed<object> result)
        {
            var helpText = HelpText.AutoBuild(
                result,
                h =>
                {
                    h.AddDashesToOption = true;
                    h.AdditionalNewLineAfterOption = false;
                    h.Heading = string.Format(Resources.Heading, Utils.GetEntryAssemblyVersion());
                    h.Copyright = Constants.MicrosoftCopyright;
                    h.AddNewLineBetweenHelpSections = true;
                    h.AddPreOptionsLine(Resources.AppDescription_HelpText);
                    h.AddPreOptionsLine(Environment.NewLine);
                    h.AddPreOptionsLine(Resources.CommandsAvailable_Message);
                    h.AddPostOptionsLines(new string[] { Resources.MoreHelp_HelpText, string.Format(Resources.PrivacyStatement_HelpText, Constants.PrivacyStatementUrl) });
                    h.MaximumDisplayWidth = 100;
                    h.AutoHelp = false;
                    h.AutoVersion = false;
                    return h;
                },
                e => e,
                verbsIndex: true);
            Console.WriteLine(helpText);
            Console.WriteLine();
        }

        private static void DisplayParsingErrors<T>(NotParsed<T> result)
        {
            if (!result.Errors.Any(
                e => e is NoVerbSelectedError ||
                (e is BadVerbSelectedError badVerbError && badVerbError.Token == "-?") ||
                (e is UnknownOptionError unknownOptionError && unknownOptionError.Token == "?")))
            {
                var builder = SentenceBuilder.Create();
                var errorMessages = HelpText.RenderParsingErrorsTextAsLines(result, builder.FormatError, builder.FormatMutuallyExclusiveSetErrors, 1);

                foreach (var error in errorMessages)
                {
                    Logger.Warn(error);
                }
            }
        }
    }
}
