// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands.DscCommands;

using Newtonsoft.Json.Linq;

/// <summary>
/// Base class for DSC commands.
/// </summary>
public abstract class BaseDscCommand
{
    /// <summary>
    /// DSC Get command.
    /// </summary>
    /// <param name="input">Input for the Get command.</param>
    public abstract void Get(JToken input);

    /// <summary>
    /// DSC Set command.
    /// </summary>
    /// <param name="input">Input for the Set command.</param>
    public abstract void Set(JToken input);

    /// <summary>
    /// DSC Test command.
    /// </summary>
    /// <param name="input">Input for the Test command.</param>
    public abstract void Test(JToken input);

    /// <summary>
    /// DSC Export command.
    /// </summary>
    /// <param name="input">Input for the Export command.</param>
    public abstract void Export(JToken input);
}
