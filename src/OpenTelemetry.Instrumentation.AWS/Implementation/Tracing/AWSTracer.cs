// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Runtime.Telemetry;
using Amazon.Runtime.Telemetry.Tracing;

namespace OpenTelemetry.Instrumentation.AWS.Implementation.Tracing;

internal sealed class AWSTracer : Tracer
{
    private readonly ActivitySource activitySource;
    private readonly bool recordException;
    private readonly bool useProducerKindForMessageSend;
    private int disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AWSTracer"/> class.
    /// </summary>
    /// <param name="activitySource">The ActivitySource used for creating and tracking the activities.</param>
    /// <param name="recordException">Whether the exception of a failed call is recorded on the span.</param>
    /// <param name="useProducerKindForMessageSend">
    /// Whether spans for sending a single message to AWS SNS or AWS SQS are created with <see cref="ActivityKind.Producer"/>
    /// when their context is injected into the message as its creation context, as described by the semantic conventions
    /// for AWS SNS and AWS SQS since v1.44.0.
    /// </param>
    public AWSTracer(ActivitySource activitySource, bool recordException, bool useProducerKindForMessageSend = false)
    {
        this.activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
        this.recordException = recordException;
        this.useProducerKindForMessageSend = useProducerKindForMessageSend;
    }

    public override TraceSpan CreateSpan(
        string name,
        Attributes? initialAttributes = null,
        SpanKind spanKind = SpanKind.INTERNAL,
        SpanContext? parentContext = null)
    {
        if (this.IsDisposed())
        {
            return AWSTraceSpan.Noop;
        }

        var tags = initialAttributes != null ? new ActivityTagsCollection(initialAttributes.AllAttributes) : null;
        var activityKind = ConvertToActivityKind(spanKind);
        ActivityLink[]? links = null;

        if (this.useProducerKindForMessageSend && activityKind == ActivityKind.Client && IsMessageSend(name))
        {
            var sendContext = AWSMessageContextPipelineHandler.SendContext;
            if (sendContext.CreationContext != default)
            {
                // A separate creation context is preserved in the message; this span describes only its transport.
                links = [new ActivityLink(sendContext.CreationContext)];
            }
            else if (sendContext.CanInjectContext)
            {
                // The context of this span becomes the creation context of the message, which is what makes it a producer span.
                // If it cannot be injected, such as when the message already has the maximum number of attributes, the span
                // is not the creation context of the message so remains a client span.
                activityKind = ActivityKind.Producer;
            }
        }

        var parentActivityContext = ConvertToActivityContext(parentContext);
        var activity = this.activitySource.StartActivity(name, activityKind, parentActivityContext, tags, links);

        return new AWSTraceSpan(activity, this.recordException);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Interlocked.Exchange(ref this.disposed, 1);
        }

        base.Dispose(disposing);
    }

    // Only spans for sending a single message are considered as the context of the span is only injected into
    // the message when a single message is sent, which the "Send span (producer)" of the semantic conventions requires.
    // See https://github.com/open-telemetry/semantic-conventions/blob/v1.45.0/docs/messaging/sqs.md#send-span-producer
    // and https://github.com/open-telemetry/semantic-conventions/blob/v1.45.0/docs/messaging/sns.md#send-span-producer.
    private static bool IsMessageSend(string name)
        => string.Equals(name, "SQS.SendMessage", StringComparison.Ordinal) ||
           string.Equals(name, "SNS.Publish", StringComparison.Ordinal);

    private static ActivityContext ConvertToActivityContext(SpanContext? parentContext)
    {
        if (parentContext == null)
        {
            return default;
        }

        var traceId = ActivityTraceId.CreateFromString(parentContext.TraceId.AsSpan());
        var spanId = ActivitySpanId.CreateFromString(parentContext.SpanId.AsSpan());

        return new ActivityContext(traceId, spanId, default, null, parentContext.IsRemote);
    }

    private static ActivityKind ConvertToActivityKind(SpanKind spanKind) => spanKind switch
    {
        SpanKind.CLIENT => ActivityKind.Client,
        SpanKind.SERVER => ActivityKind.Server,
        SpanKind.PRODUCER => ActivityKind.Producer,
        SpanKind.CONSUMER => ActivityKind.Consumer,
        SpanKind.INTERNAL => ActivityKind.Internal,
        _ => ActivityKind.Internal,
    };

    private bool IsDisposed()
        => Volatile.Read(ref this.disposed) != 0;
}
