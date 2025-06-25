// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.WingetCreateCLI.Commands.DscCommands;
using Microsoft.WingetCreateCLI.Logging;
using Microsoft.WingetCreateCLI.Properties;
using Newtonsoft.Json.Linq;

/// <summary>
/// Command for managing the application using dsc v3.
/// </summary>
[Verb("dsc", HelpText = "DscCommand_HelpText", ResourceType = typeof(Resources))]
public class DscCommand : BaseCommand
{
    /// <inheritdoc/>
    public override bool AcceptsGitHubToken => false;

    /// <summary>
    /// Gets or sets the name of the resource to be managed by the dsc command.
    /// </summary>
    [Value(0, MetaName = "ResourceName", Required = true, HelpText = "DscResourceName_HelpText", ResourceType = typeof(Resources))]
    public string ResourceName { get; set; }

    /// <summary>
    /// Gets or sets the input for the dsc command.
    /// </summary>
    [Value(1, MetaName = "Input", Required = false, HelpText = "DscInput_HelpText", ResourceType = typeof(Resources))]
    public string Input { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to execute the dsc Get operation.
    /// </summary>
    [Option('g', "get", SetName = "GetMethod", HelpText = "DscGet_HelpText", ResourceType = typeof(Resources))]
    public bool Get { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to execute the dsc Set operation.
    /// </summary>
    [Option('s', "set", SetName = "SetMethod", HelpText = "DscSet_HelpText", ResourceType = typeof(Resources))]
    public bool Set { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to execute the dsc Test operation.
    /// </summary>
    [Option('t', "test", SetName = "TestMethod", HelpText = "DscTest_HelpText", ResourceType = typeof(Resources))]
    public bool Test { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to execute the dsc Export operation.
    /// </summary>
    [Option('e', "export", SetName = "ExportMethod", HelpText = "DscExport_HelpText", ResourceType = typeof(Resources))]
    public bool Export { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to execute the schema command.
    /// </summary>
    [Option("schema", SetName = "SchemaMethod", HelpText = "DscSchema_HelpText", ResourceType = typeof(Resources))]
    public bool Schema { get; set; }

    /// <summary>
    /// Executes the dsc command flow.
    /// </summary>
    /// <returns>Boolean representing success or fail of the command.</returns>
    public override async Task<bool> Execute()
    {
        await Task.CompletedTask;

        if (!BaseDscCommand.TryCreateInstance(this.ResourceName, out var dscCommand))
        {
            var availableResources = string.Join(", ", BaseDscCommand.GetAvailableCommands());
            Logger.ErrorLocalized(nameof(Resources.DscResourceNameNotFound_Message), this.ResourceName, availableResources);
            return false;
        }

        try
        {
            var input = string.IsNullOrWhiteSpace(this.Input) ? null : JToken.Parse(this.Input);
            var operations = new (bool, Func<bool>)[]
            {
                (this.Get,    () => dscCommand.Get(input)),
                (this.Set,    () => dscCommand.Set(input)),
                (this.Test,   () => dscCommand.Test(input)),
                (this.Export, () => dscCommand.Export(input)),
                (this.Schema, () => dscCommand.Schema()),
            };

            foreach (var (methodFlag, methodAction) in operations)
            {
                if (methodFlag)
                {
                    if (!methodAction())
                    {
                        Logger.ErrorLocalized(nameof(Resources.DscResourceOperationFailed_Message));
                        return false;
                    }

                    return true;
                }
            }

            Logger.ErrorLocalized(nameof(Resources.DscResourceOperationNotSpecified_Message));
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message);
            return false;
        }
    }
}
