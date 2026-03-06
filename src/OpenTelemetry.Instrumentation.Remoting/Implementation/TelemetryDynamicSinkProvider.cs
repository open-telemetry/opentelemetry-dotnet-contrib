// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Remoting.Contexts;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.Remoting.Implementation;

/// <summary>
/// A <see cref="IContributeDynamicSink"/> implementation that returns an instance of
/// <see cref="TelemetryDynamicSink"/> responsible for instrumenting remoting calls.
/// </summary>
internal sealed class TelemetryDynamicSinkProvider : IDynamicProperty, IContributeDynamicSink, IDisposable
{
    internal const string DynamicPropertyName = "TelemetryDynamicSinkProvider";

    private readonly ActivitySource activitySource = CreateActivitySource();
    private readonly ConcurrentDictionary<string, string> serviceNameCache = new();

    private readonly RemotingInstrumentationOptions options;

    public TelemetryDynamicSinkProvider(RemotingInstrumentationOptions options)
    {
        this.options = options;
    }

    /// <inheritdoc />
    public string Name => DynamicPropertyName;

    /// <summary>
    /// Creates and returns a <see cref="TelemetryDynamicSink"/> to be used for instrumentation.
    /// </summary>
    /// <returns>A new instance of <see cref="TelemetryDynamicSink"/>.</returns>
    public IDynamicMessageSink GetDynamicSink() => new TelemetryDynamicSink(this.options, this.activitySource, this.serviceNameCache);

    /// <inheritdoc />
    public void Dispose() => this.activitySource.Dispose();

    private static ActivitySource CreateActivitySource()
    {
        const string telemetrySchemaUrl = "https://opentelemetry.io/schemas/1.40.0";
        var assembly = typeof(TelemetryDynamicSink).Assembly;
        var version = assembly.GetPackageVersion();

        var activitySourceOptions = new ActivitySourceOptions(TelemetryDynamicSink.ActivitySourceName)
        {
            Version = version,
            TelemetrySchemaUrl = telemetrySchemaUrl,
        };

        return new ActivitySource(activitySourceOptions);
    }
}
