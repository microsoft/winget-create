// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Models.DscModels;

/// <summary>
/// Specifies the severity level of a message.
/// </summary>
public enum DscMessageLevel
{
    /// <summary>
    /// Represents an error message.
    /// </summary>
    Error,

    /// <summary>
    /// Represents a warning message.
    /// </summary>
    Warning,

    /// <summary>
    /// Represents an informational message.
    /// </summary>
    Info,

    /// <summary>
    /// Represents a debug message.
    /// </summary>
    Debug,

    /// <summary>
    /// Represents a trace message.
    /// </summary>
    Trace,
}
