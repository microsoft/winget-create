// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using CommandLine;
    using CommandLine.Text;
    using Microsoft.WingetCreateCLI.Commands;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCLI.Telemetry;
    using Microsoft.WingetCreateCLI.Telemetry.Events;
    using Microsoft.WingetCreateCore.Common;

    /// <summary>
    /// Main entry class for the CLI.
    /// </summary>
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            Logger.Initialize();
            UserSettings.FirstRunTelemetryConsent();
            TelemetryEventListener.EventListener.IsTelemetryEnabled();

            string arguments = string.Join(' ', Environment.GetCommandLineArgs());
            Logger.Trace($"Command line args: {arguments}");

            Parser myParser = new Parser(config => config.HelpWriter = null);

            var types = new Type[] { typeof(NewCommand), typeof(UpdateCommand), typeof(SubmitCommand), typeof(SettingsCommand), typeof(TokenCommand), typeof(CacheCommand) };
            var parserResult = myParser.ParseArguments(args, types);

            BaseCommand command = parserResult.MapResult(c => c as BaseCommand, err => null);
            if (!await command.LoadGitHubClient())
            {
                return 1;
            }

            try
            {
                string latestVersion = await GitHub.GetLatestRelease();
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

            if (command == null)
            {
                DisplayHelp(parserResult as NotParsed<object>);
                DisplayParsingErrors(parserResult as NotParsed<object>);
                return args.Any() ? 1 : 0;
            }

            try
            {
                WingetCreateCore.Serialization.ProducedBy = string.Join(" ", Constants.ProgramName, Utils.GetEntryAssemblyVersion());
                return await command.Execute() ? 0 : 1;
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
        }

        private static void DisplayHelp(NotParsed<object> result)
        {
            var helpText = HelpText.AutoBuild(
                result,
                h =>
                {
                    h.AddDashesToOption = true;
                    h.AdditionalNewLineAfterOption = false;
                    h.Heading = string.Format(Resources.Heading, Utils.GetEntryAssemblyVersion()) + Environment.NewLine;
                    h.Copyright = Constants.MicrosoftCopyright;
                    h.AddNewLineBetweenHelpSections = true;
                    h.AddPreOptionsLine(Resources.AppDescription_HelpText);
                    h.AddPostOptionsLines(new string[] { Resources.MoreHelp_HelpText, Resources.PrivacyStatement_HelpText });
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

        private static void DisplayParsingErrors<T>(ParserResult<T> result)
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
