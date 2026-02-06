// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.Remoting.Messaging;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Instrumentation.Remoting;

/// <summary>
/// Options for .NET Remoting instrumentation.
/// </summary>
public class RemotingInstrumentationOptions
{
    /// <summary>
    /// Gets or sets an action to enrich an Activity.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Activity"/>: the activity being enriched.</para>
    /// <para>string: the name of the event. See <see cref="RemotingInstrumentationEnrichEventNames"/> for available values.</para>
    /// <para><see cref="IMethodMessage"/>: the Remoting method message from which additional information can be extracted to enrich the activity.</para>
    /// </remarks>
    public Action<Activity, string, IMethodMessage>? Enrich { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="TextMapPropagator"/> for context propagation. Default value: <see cref="CompositeTextMapPropagator"/> with <see cref="TraceContextPropagator"/> and <see cref="BaggagePropagator"/>.
    /// </summary>
    public TextMapPropagator Propagator { get; set; } = new CompositeTextMapPropagator(new TextMapPropagator[]
    {
        new TraceContextPropagator(),
        new BaggagePropagator(),
    });

    /// <summary>
    /// Gets or sets a filter function that determines whether or not to collect telemetry about a Remoting message.
    /// </summary>
    /// <remarks>
    /// The filter function receives an <see cref="IMessage"/> and should return a boolean.
    /// <list type="bullet">
    /// <item>If the filter returns <see langword="true"/>, the message is instrumented.</item>
    /// <item>If the filter returns <see langword="false"/> or throws an exception, no instrumentation is performed.</item>
    /// </list>
    /// </remarks>
    public Func<IMessage, bool>? Filter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether exception will be recorded
    /// as an <see cref="ActivityEvent"/> or not. Default value: <see
    /// langword="true"/>.
    /// </summary>
    /// <remarks>
    /// <para>For specification details see: <see
    /// href="https://github.com/open-telemetry/semantic-conventions/blob/main/docs/exceptions/exceptions-spans.md"
    /// />.</para>
    /// </remarks>
    public bool RecordException { get; set; }
}
