// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources.Host;

/// <summary>
/// Options for the host resource detector.
/// </summary>
public sealed class HostDetectorOptions
{
    internal const string EnableNetworkAddressesEnvVarName = "OTEL_DOTNET_EXPERIMENTAL_HOST_RESOURCE_ENABLE_NETWORK_ADDRESSES";
    internal const string EnableCpuInfoEnvVarName = "OTEL_DOTNET_EXPERIMENTAL_HOST_RESOURCE_ENABLE_CPU_INFO";

    /// <summary>
    /// Initializes a new instance of the <see cref="HostDetectorOptions"/> class.
    /// </summary>
    public HostDetectorOptions()
    {
        this.EnableNetworkAddresses = IsEnabled(EnableNetworkAddressesEnvVarName);
        this.EnableCpuInfo = IsEnabled(EnableCpuInfoEnvVarName);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the <c>host.ip</c> and <c>host.mac</c>
    /// attributes are emitted.
    /// <para/>
    /// The default value is read from the
    /// <c>OTEL_DOTNET_EXPERIMENTAL_HOST_RESOURCE_ENABLE_NETWORK_ADDRESSES</c>
    /// environment variable, and is <see langword="false"/> when it is not set to <c>true</c>.
    /// </summary>
    public bool EnableNetworkAddresses { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the <c>host.cpu.*</c> attributes are emitted.
    /// <para/>
    /// The default value is read from the
    /// <c>OTEL_DOTNET_EXPERIMENTAL_HOST_RESOURCE_ENABLE_CPU_INFO</c>
    /// environment variable, and is <see langword="false"/> when it is not set to <c>true</c>.
    /// </summary>
    public bool EnableCpuInfo { get; set; }

    private static bool IsEnabled(string envVarName) =>
        bool.TryParse(Environment.GetEnvironmentVariable(envVarName), out var enabled) && enabled;
}
