// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.WingetCreateCLI.Commands.DscCommands;
using Microsoft.WingetCreateCLI.Logging;
using Newtonsoft.Json.Linq;

/// <summary>
/// Command for managin the application using dsc v3.
/// </summary>
[Verb("dsc", HelpText = "Manage the application using dsc v3.")]
public class DscCommand : BaseCommand
{
    /// <inheritdoc/>
    public override bool RequiresGitHubToken => false;

    /// <summary>
    /// Gets or sets the unbound arguments that exist after the positional parameters.
    /// </summary>
    [Value(2, Hidden = true)]
    public IList<string> UnboundArgs { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the input for the dsc Get operation.
    /// </summary>
    [Option('g', "get", SetName = "GetMethod", HelpText = "Command for the Get flow.")]
    public string Get { get; set; }

    /// <summary>
    /// Gets or sets the input for the dsc Set operation.
    /// </summary>
    [Option('s', "set", SetName = "SetMethod", HelpText = "Command for the Set flow.")]
    public string Set { get; set; }

    /// <summary>
    /// Gets or sets the input for the dsc Test operation.
    /// </summary>
    [Option('t', "test", SetName = "TestMethod", HelpText = "Command for the Test flow.")]
    public string Test { get; set; }

    /// <summary>
    /// Gets or sets the input for the dsc Export operation.
    /// </summary>
    [Option('e', "export", SetName = "ExportMethod", HelpText = "Command for the Export flow.")]
    public string Export { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to execute the schema command.
    /// </summary>
    [Option("schema", SetName = "SchemaMethod", HelpText = "Command for the Schema flow.")]
    public bool Schema { get; set; }

    /// <summary>
    /// Executes the dsc command flow.
    /// </summary>
    /// <returns>Boolean representing success or fail of the command.</returns>
    public override async Task<bool> Execute()
    {
        await Task.CompletedTask;

        List<BaseDscCommand> dscCommands = [new DscSettingsCommand()];
        var argCommandName = this.UnboundArgs.FirstOrDefault();
        var dscCommand = dscCommands.FirstOrDefault(c => c.CommandName.Equals(argCommandName, StringComparison.OrdinalIgnoreCase));

        if (dscCommand == null)
        {
            Logger.Error($"Unknown DSC resource {argCommandName}");
            return false;
        }

        if (this.HandleOperation("Get", this.Get, (input) => dscCommand.Get(input)) ||
           this.HandleOperation("Set", this.Set, (input) => dscCommand.Set(input)) ||
           this.HandleOperation("Test", this.Test, (input) => dscCommand.Test(input)) ||
           this.HandleOperation("Export", this.Export, (input) => dscCommand.Export(input)) ||
           (this.Schema && dscCommand.Schema()))
        {
            return true;
        }

        Logger.Error("No valid DSC command provided.");
        return false;
    }

    private bool HandleOperation(string name, string arg, Func<JToken, bool> op)
    {
        if (arg == null)
        {
            // If no argument is provided, then we assume another operation is being requested.
            return false;
        }

        try
        {
            var input = this.GetJsonOrNull(arg);
            if (!op(input))
            {
                Logger.Error($"Invalid input for {name} command.");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message);
            return false;
        }
    }

    private JToken GetJsonOrNull(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JToken.Parse(json);
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message);
            return null;
        }
    }
}
