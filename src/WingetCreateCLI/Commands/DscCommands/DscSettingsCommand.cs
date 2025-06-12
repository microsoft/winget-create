// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands.DscCommands;

using Microsoft.WingetCreateCLI.Models.DscModels;
using Newtonsoft.Json.Linq;

/// <summary>
/// Command for managing the settings using dsc v3.
/// </summary>
public class DscSettingsCommand : BaseDscCommand
{
    /// <inheritdoc/>
    public override string CommandName => "settings";

    /// <inheritdoc/>
    public override void Get(JToken input)
    {
        this.Export(input);
    }

    /// <inheritdoc/>
    public override void Set(JToken input)
    {
        var data = new SettingsFunctionData(input);
        data.Get();

        // Capture the diff before updating the output
        var diff = data.DiffJson();

        if (!data.Test())
        {
            data.Output.Settings = data.GetResolvedInput();
            data.WriteOutput();
        }

        this.WriteJsonOutputLine(data.Output.ToJson());
        this.WriteJsonOutputLine(diff);
    }

    /// <inheritdoc/>
    public override void Test(JToken input)
    {
        var data = new SettingsFunctionData(input);

        data.Get();
        data.Output.InDesiredState = data.Test();

        this.WriteJsonOutputLine(data.Output.ToJson());
        this.WriteJsonOutputLine(data.DiffJson());
    }

    /// <inheritdoc/>
    public override void Export(JToken input)
    {
        var data = new SettingsFunctionData();

        data.Get();

        this.WriteJsonOutputLine(data.Output.ToJson());
    }

    /// <inheritdoc/>
    public override void Schema()
    {
        this.WriteJsonOutputLine(this.CreateSchema<SettingsResourceObject>());
    }
}
