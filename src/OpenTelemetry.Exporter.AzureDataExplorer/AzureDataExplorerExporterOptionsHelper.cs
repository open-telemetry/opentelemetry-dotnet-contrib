// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

internal static class AzureDataExplorerExporterOptionsHelper
{
    public static void RegisterNamedOptions<TOptions>(IServiceCollection services, string? name, Action<TOptions>? configure)
        where TOptions : class
    {
        services.AddOptions();

        if (name != null && configure != null)
        {
            // Named options participate in the options pipeline, so they can
            // also be bound from configuration by the application.
            services.Configure(name, configure);
        }
    }

#if NET
    public static T GetOptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(IServiceProvider serviceProvider, string? name)
#else
    public static T GetOptions<T>(IServiceProvider serviceProvider, string? name)
#endif
        where T : class
    {
        var finalName = name ?? Options.DefaultName;

        // Unnamed options always use a fresh instance so delegates configured
        // for one signal do not leak into another signal.
        return name == null
            ? serviceProvider.GetRequiredService<IOptionsFactory<T>>().Create(finalName)
            : serviceProvider.GetRequiredService<IOptionsMonitor<T>>().Get(finalName);
    }

    public static AzureDataExplorerExporterOptions GetExporterOptions(IServiceProvider serviceProvider, string? name, Action<AzureDataExplorerExporterOptions>? configure)
    {
        var options = GetOptions<AzureDataExplorerExporterOptions>(serviceProvider, name);
        if (name == null)
        {
            configure?.Invoke(options);
        }

        return options;
    }
}
