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
        /// <summary>
        /// Displays the application header and the copyright.
        /// </summary>
        public static void DisplayApplicationHeaderAndCopyright()
        {
            Console.WriteLine(string.Format(
                Resources.Heading,
                Utils.GetEntryAssemblyVersion()) +
                Environment.NewLine +
                Constants.MicrosoftCopyright);
        }

        private static async Task<int> Main(string[] args)
        {
            Logger.Initialize();
            UserSettings.FirstRunTelemetryConsent();
            TelemetryEventListener.EventListener.IsTelemetryEnabled();
            SentenceBuilder.Factory = () => new LocalizableSentenceBuilder();

            string arguments = string.Join(' ', Environment.GetCommandLineArgs());
            Logger.Trace($"Command line args: {arguments}");

            Parser myParser = new Parser(config =>
            {
                config.HelpWriter = null;
                config.CaseSensitive = false;
            });

            var types = new Type[]
            {
                typeof(NewCommand),
                typeof(UpdateCommand),
                typeof(SubmitCommand),
                typeof(SettingsCommand),
                typeof(TokenCommand),
                typeof(CacheCommand),
                typeof(ShowCommand),
            };

            var baseCommandsParserResult = myParser.ParseArguments(args, types);
            BaseCommand parsedCommand = baseCommandsParserResult.MapResult(c => c as BaseCommand, err => null);

            if (parsedCommand == null)
            {
                // In case of unsuccessful parsing, check if user tried to run a valid command.
                bool isBaseCommand = baseCommandsParserResult.TypeInfo.Current.BaseType.Equals(typeof(BaseCommand));

                /* Parse root options.
                 * Adding '--help' is a workaround to force parser to display the options help text when no arguments are passed.
                 * This makes rootOptionsParserResult to be a NotParsed object which makes HelpText.AutoBuild print the correct help text.
                 * This is done since the parser does not print the correct help text on a successful parse.
                  */
                ParserResult<RootOptions> rootOptionsParserResult = myParser.ParseArguments<RootOptions>(
                    args.Length == 0 ? new string[] { "--help" } : args);

                if (!isBaseCommand)
                {
                    rootOptionsParserResult.WithParsed(RootOptions.ParseRootOptions);

                    if (rootOptionsParserResult.Tag == ParserResultType.Parsed)
                    {
                        return 0;
                    }
                }

                DisplayHelp(baseCommandsParserResult as NotParsed<object>, isBaseCommand ? null : rootOptionsParserResult as NotParsed<RootOptions>);
                DisplayParsingErrors(baseCommandsParserResult as NotParsed<object>);
                return args.Any() ? 1 : 0;
            }

            if (parsedCommand is not SettingsCommand && parsedCommand is not CacheCommand)
            {
                // Do not load github client for settings or cache command.
                if (await parsedCommand.LoadGitHubClient())
                {
                    try
                    {
                        string latestVersion = await parsedCommand.GitHubClient.GetLatestRelease();
                        string trimmedVersion = latestVersion.TrimStart('v').Split('-').First();
                        if (trimmedVersion != Utils.GetEntryAssemblyVersion())
                        {
                            Logger.WarnLocalized(nameof(Resources.OutdatedVersionNotice_Message));
                            Logger.WarnLocalized(nameof(Resources.GetLatestVersion_Message), latestVersion, "https://github.com/microsoft/winget-create/releases");
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
                    if (parsedCommand is not NewCommand)
                    {
                        return 1;
                    }
                }
            }

            try
            {
                WingetCreateCore.Serialization.ProducedBy = string.Join(" ", Constants.ProgramName, Utils.GetEntryAssemblyVersion());
                return await parsedCommand.Execute() ? 0 : 1;
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
                    Common.CleanUpFilesOlderThan(Path.Combine(Common.LocalAppStatePath, "DiagOutputDir"), UserSettings.CleanUpDays);
                }
            }
        }

        private static void DisplayHelp(NotParsed<object> baseCommandsParserResult, ParserResult<RootOptions> rootOptionsParserResult)
        {
            DisplayApplicationHeaderAndCopyright();
            DisplayCommandsHelpText(baseCommandsParserResult);
            if (rootOptionsParserResult != null)
            {
                DisplayRootOptionsHelpText(rootOptionsParserResult);
            }

            DisplayFooter();
        }

        private static void DisplayCommandsHelpText(NotParsed<object> result)
        {
            var helpText = HelpText.AutoBuild(
            result,
            h =>
            {
                h.AddDashesToOption = true;
                h.AdditionalNewLineAfterOption = false;
                h.Heading = string.Empty;
                h.Copyright = string.Empty;
                h.AddNewLineBetweenHelpSections = true;
                h.AddPreOptionsLine(Resources.AppDescription_HelpText);
                h.AddPreOptionsLine(Environment.NewLine);
                h.AddPreOptionsLine(Resources.CommandsAvailable_Message);
                h.MaximumDisplayWidth = 100;
                h.AutoHelp = false;
                h.AutoVersion = false;
                return h;
            },
            e => e,
            verbsIndex: true);
            Console.WriteLine(helpText);
        }

        private static void DisplayRootOptionsHelpText(ParserResult<RootOptions> result)
        {
            var helpText = HelpText.AutoBuild(
            result,
            h =>
            {
                h.AddDashesToOption = true;
                h.AdditionalNewLineAfterOption = false;
                h.Heading = Resources.OptionsAvailable_Message;
                h.Copyright = string.Empty;
                h.AddNewLineBetweenHelpSections = false;
                h.MaximumDisplayWidth = 100;
                h.AutoHelp = false;
                h.AutoVersion = false;
                return h;
            },
            e => e);
            Console.WriteLine(helpText);
        }

        private static void DisplayFooter()
        {
            Console.WriteLine(Resources.MoreHelp_HelpText);
            Console.WriteLine(Resources.PrivacyStatement_HelpText);
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
