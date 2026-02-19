// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.Remoting.Contexts;

namespace OpenTelemetry.Instrumentation.Remoting.Implementation;

/// <summary>
/// A <see cref="IContributeDynamicSink"/> implementation that returns an instance of
/// <see cref="TelemetryDynamicSink"/> responsible for instrumenting remoting calls.
/// </summary>
internal sealed class TelemetryDynamicSinkProvider : IDynamicProperty, IContributeDynamicSink
{
    internal const string DynamicPropertyName = "TelemetryDynamicSinkProvider";

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
    public IDynamicMessageSink GetDynamicSink() => new TelemetryDynamicSink(this.options);
}
