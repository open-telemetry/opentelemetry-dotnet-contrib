// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Lambda.Core;
using OpenTelemetry.AWS;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Instrumentation.AWSLambda;

/// <summary>
/// AWS lambda instrumentation options.
/// </summary>
public class AWSLambdaInstrumentationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether AWS X-Ray context extraction should be disabled.
    /// </summary>
    public bool DisableAwsXRayContextExtraction { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the parent Activity should be set when a potentially batched event is received where multiple parents are potentially available (e.g. SQS).
    /// If set to true, the parent is set using the last received record (e.g. last message). Otherwise the parent is not set. In both cases, links will be created for such events.
    /// </summary>
    /// <remarks>
    /// Currently, the only event type to which this applies is SQS.
    /// </remarks>
    public bool SetParentFromBatch { get; set; }

    /// <summary>
    /// Gets or sets an action to enrich the invocation Activity from the function input and
    /// the Lambda context. Use it to describe a trigger the instrumentation does not recognise,
    /// for example to set <c>faas.trigger</c> and the <c>faas.document.*</c> attributes for an
    /// Amazon S3 event.
    /// </summary>
    /// <remarks>
    /// The action runs after the activity is created, so values it sets are not seen by samplers,
    /// and it applies process-wide rather than per-provider.
    /// </remarks>
    public Action<Activity, object?, ILambdaContext>? EnrichWithInput { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="Logs.LoggerProvider"/> to flush at the end of each
    /// invocation. Log records are not flushed when it is not set.
    /// </summary>
    /// <remarks>
    /// Must be built and set before the first invocation.
    /// </remarks>
    public LoggerProvider? LoggerProvider { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="Metrics.MeterProvider"/> to flush at the end of each
    /// invocation. Metrics are not flushed when it is not set.
    /// </summary>
    /// <remarks>
    /// Must be built and set before the first invocation. Prefer delta temporality, since every
    /// invocation flushes.
    /// </remarks>
    public MeterProvider? MeterProvider { get; set; }

    /// <summary>
    /// Gets or sets the timeout, in milliseconds, for flushing at the end of an invocation.
    /// Default value is 10000. Set to <see cref="Timeout.Infinite"/> to wait indefinitely.
    /// </summary>
    /// <remarks>
    /// Applied to each provider, which are flushed concurrently, and to the overall wait.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is zero, or negative and not <see cref="Timeout.Infinite"/>.
    /// </exception>
    public int FlushTimeoutMilliseconds
    {
        get;
        set
        {
            // Zero is rejected because it would abandon every flush rather than wait briefly.
            if (value != Timeout.Infinite)
            {
                Guard.ThrowIfOutOfRange(value, min: 1);
            }

            field = value;
        }
#pragma warning disable SA1500, SA1513
    } = 10000;
#pragma warning restore SA1500, SA1513

    /// <inheritdoc cref="AWSLambda.SemanticConventionVersion"/>
    public SemanticConventionVersion SemanticConventionVersion { get; set; } = AWSSemanticConventions.DefaultSemanticConventionVersion;
}
