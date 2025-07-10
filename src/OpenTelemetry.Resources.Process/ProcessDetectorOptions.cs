// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources.Process;

/// <summary>
/// Provides configuration options for the process resource detector.
/// </summary>
/// <remarks>Use this class to specify settings that control the behavior of the process resource detector.</remarks>
public class ProcessDetectorOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether should the command used to start the process should be included as a resource attribute.
    /// </summary>
    /// <remarks>Should only be set to <c>true</c> if non-sensitive data will not be passed. Defaults to <c>false</c>.</remarks>
    public bool IncludeCommand { get; set; }
}
