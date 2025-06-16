// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands.DscCommands;

using System;
using Microsoft.WingetCreateCLI.Logging;
using Microsoft.WingetCreateCLI.Models.DscModels;
using Microsoft.WingetCreateCLI.Properties;
using Newtonsoft.Json.Linq;

/// <summary>
/// Command for managing the settings using dsc v3.
/// </summary>
public class DscSettingsCommand : BaseDscCommand
{
    /// <inheritdoc/>
    public override string CommandName => "settings";

    /// <inheritdoc/>
    public override bool Get(JToken input)
    {
        return this.Export(input);
    }

    /// <inheritdoc/>
    public override bool Set(JToken input)
    {
        if (input == null)
        {
            Logger.ErrorLocalized(nameof(Resources.DscInputRequired_Message), nameof(this.Set));
            return false;
        }

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
        return true;
    }

    /// <inheritdoc/>
    public override bool Test(JToken input)
    {
        if (input == null)
        {
            Logger.ErrorLocalized(nameof(Resources.DscInputRequired_Message), nameof(this.Test));
            return false;
        }

        var data = new SettingsFunctionData(input);

        data.Get();
        data.Output.InDesiredState = data.Test();

        this.WriteJsonOutputLine(data.Output.ToJson());
        this.WriteJsonOutputLine(data.DiffJson());
        return true;
    }

    /// <inheritdoc/>
    public override bool Export(JToken input)
    {
        var data = new SettingsFunctionData();
        data.Get();
        this.WriteJsonOutputLine(data.Output.ToJson());
        return true;
    }

    /// <inheritdoc/>
    public override bool Schema()
    {
        this.WriteJsonOutputLine(this.CreateSchema<SettingsResourceObject>());
        return true;
    }
}
