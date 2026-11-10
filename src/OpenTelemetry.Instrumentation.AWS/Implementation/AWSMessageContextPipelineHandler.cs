// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using OpenTelemetry.Context.Propagation;
using SNS = Amazon.SimpleNotificationService.Model;
using SQS = Amazon.SQS.Model;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

/// <summary>
/// Makes the message creation context of a message that is being sent available before the AWS SDK creates its send span.
/// </summary>
internal sealed class AWSMessageContextPipelineHandler : PipelineHandler
{
    // The context of the span is not available until the span is created, so valid identifiers are used
    // to find the attributes that the propagator would inject into the message.
    private static readonly ActivityTraceId RepresentativeTraceId = ActivityTraceId.CreateFromString("00000000000000000000000000000001".AsSpan());
    private static readonly ActivitySpanId RepresentativeSpanId = ActivitySpanId.CreateFromString("0000000000000001".AsSpan());

    private static readonly AsyncLocal<MessageSendContext> SendContextSlot = new();

    /// <summary>
    /// Gets the context for the message that is being sent by the current request.
    /// </summary>
    internal static MessageSendContext SendContext => SendContextSlot.Value;

    public override void InvokeSync(IExecutionContext executionContext)
    {
        if (!TryGetSendContext(executionContext.RequestContext.OriginalRequest, out var sendContext))
        {
            // Requests that do not send a message have nothing to make available.
            base.InvokeSync(executionContext);
            return;
        }

        var previousContext = SendContextSlot.Value;

        try
        {
            SendContextSlot.Value = sendContext;
            base.InvokeSync(executionContext);
        }
        finally
        {
            SendContextSlot.Value = previousContext;
        }
    }

    public override Task<T> InvokeAsync<T>(IExecutionContext executionContext)
        => TryGetSendContext(executionContext.RequestContext.OriginalRequest, out var sendContext)
            ? this.InvokeWithSendContextAsync<T>(executionContext, sendContext)
            : base.InvokeAsync<T>(executionContext); // Requests that do not send a message have nothing to make available.

    /// <summary>
    /// Gets the context for the message that is sent by a request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="sendContext">When this method returns, the context if the request sends a message.</param>
    /// <returns>
    /// <see langword="true"/> if the request sends a single message; otherwise <see langword="false"/>.
    /// </returns>
    internal static bool TryGetSendContext(AmazonWebServiceRequest request, out MessageSendContext sendContext)
    {
        if (request is not (SQS.SendMessageRequest or SNS.PublishRequest))
        {
            sendContext = default;
            return false;
        }

        try
        {
            sendContext = GetSendContext(request);
        }
        catch (Exception)
        {
            // Instrumentation must not fail the request: without a context to link to
            // or one that is known to be injected, the span remains a client span.
            sendContext = default;
        }

        return true;
    }

    internal static ActivityContext ExtractCreationContext(AmazonWebServiceRequest request)
        => request switch
        {
            SQS.SendMessageRequest { MessageAttributes: { } attributes } => Propagators.DefaultTextMapPropagator.Extract(
                default,
                attributes,
                static (carrier, key) => carrier.TryGetValue(key, out var value) && value?.StringValue is { } stringValue && !SqsRequestContextHelper.IsInjectedByInstrumentation(value) ? [stringValue] : []).ActivityContext,
            SNS.PublishRequest { MessageAttributes: { } attributes } => Propagators.DefaultTextMapPropagator.Extract(
                default,
                attributes,
                static (carrier, key) => carrier.TryGetValue(key, out var value) && value?.StringValue is { } stringValue && !SnsRequestContextHelper.IsInjectedByInstrumentation(value) ? [stringValue] : []).ActivityContext,
            _ => default,
        };

    private static MessageSendContext GetSendContext(AmazonWebServiceRequest request)
    {
        var creationContext = ExtractCreationContext(request);

        if (creationContext != default)
        {
            return new(creationContext, canInjectContext: false);
        }

        var canInjectContext = request switch
        {
            SQS.SendMessageRequest sqsRequest => SqsRequestContextHelper.CanInjectContext(sqsRequest.MessageAttributes, GetAttributesToInject()),
            SNS.PublishRequest snsRequest => SnsRequestContextHelper.CanInjectContext(snsRequest.MessageAttributes, GetAttributesToInject()),
            _ => false,
        };

        return new(creationContext, canInjectContext);
    }

    private static IReadOnlyDictionary<string, string> GetAttributesToInject()
    {
        // Gets the attributes that would be injected into the message for the span that is about to be created.
        // The span inherits the trace state of the current activity, which the propagator injects as a further
        // attribute, so it is included. This is a best effort: the trace state of a span can only differ
        // from that if a sampler changes it, or if the span is created with an explicit parent.
        var context = new ActivityContext(
            RepresentativeTraceId,
            RepresentativeSpanId,
            ActivityTraceFlags.Recorded,
            Activity.Current?.TraceStateString);

        return AWSMessagingUtils.InjectIntoDictionary(new PropagationContext(context, Baggage.Current));
    }

    private async Task<T> InvokeWithSendContextAsync<T>(IExecutionContext executionContext, MessageSendContext sendContext)
        where T : AmazonWebServiceResponse, new()
    {
        var previousContext = SendContextSlot.Value;

        try
        {
            SendContextSlot.Value = sendContext;
            return await base.InvokeAsync<T>(executionContext).ConfigureAwait(false);
        }
        finally
        {
            SendContextSlot.Value = previousContext;
        }
    }

    /// <summary>
    /// The context of a message that is being sent, which determines how the span for sending it is created.
    /// </summary>
    internal readonly struct MessageSendContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MessageSendContext"/> struct.
        /// </summary>
        /// <param name="creationContext">The creation context that already exists for the message.</param>
        /// <param name="canInjectContext">Whether the context of the span for sending the message will be injected into it.</param>
        public MessageSendContext(ActivityContext creationContext, bool canInjectContext)
        {
            this.CreationContext = creationContext;
            this.CanInjectContext = canInjectContext;
        }

        /// <summary>
        /// Gets the creation context that already exists for the message, which was supplied by the application,
        /// or <see langword="default"/> if there is none.
        /// </summary>
        public ActivityContext CreationContext { get; }

        /// <summary>
        /// Gets a value indicating whether the context of the span for sending the message
        /// will be injected into it as its creation context.
        /// </summary>
        public bool CanInjectContext { get; }
    }
}
