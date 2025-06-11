// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.WingetCreateCLI.Commands.DscCommands;
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
    /// Executes the dsc command flow.
    /// </summary>
    /// <returns>Boolean representing success or fail of the command.</returns>
    public override async Task<bool> Execute()
    {
        BaseDscCommand dscCommand;
        var dscScope = this.UnboundArgs.FirstOrDefault()?.ToLowerInvariant() ?? string.Empty;
        if (dscScope == DscSettingsCommand.CommandName)
        {
            dscCommand = new DscSettingsCommand();
        }
        else
        {
            Console.WriteLine($"Unknown DSC scope: {dscScope}");
            return false;
        }

        JToken input;
        if (this.TryParse(this.Get, out input))
        {
            dscCommand.Get(input);
        }
        else if (this.TryParse(this.Set, out input))
        {
            dscCommand.Set(input);
        }
        else if (this.TryParse(this.Test, out input))
        {
            dscCommand.Test(input);
        }
        else if (this.TryParse(this.Export, out input))
        {
            dscCommand.Export(input);
        }
        else
        {
            Console.WriteLine("No valid DSC command provided. Use -g, -s, -t, or -e to specify a command.");
            return false;
        }

        return true;
    }

    private bool TryParse(string json, out JToken token)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            token = null;
            return false;
        }

        try
        {
            token = JToken.Parse(json);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing JSON: {ex.Message}");
            token = null;
            return false;
        }
    }
}
