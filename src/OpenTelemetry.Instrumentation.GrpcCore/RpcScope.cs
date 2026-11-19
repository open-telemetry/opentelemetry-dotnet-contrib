// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Google.Protobuf;
using Grpc.Core;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;
using StatusCode = Grpc.Core.StatusCode;

namespace OpenTelemetry.Instrumentation.GrpcCore;

/// <summary>
/// A class to help track the lifetime of an RPC.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
internal abstract class RpcScope<TRequest, TResponse> : IDisposable
    where TRequest : class
    where TResponse : class
{
    /// <summary>
    /// The maximum number of message events recorded for a single RPC.
    /// The number of messages on a stream is controlled by the peer, so the
    /// events are capped to stop an open streaming call growing the activity without bound.
    /// This matches the default span event count limit of the OpenTelemetry specification.
    /// </summary>
    internal const int MaxMessageEvents = 128;

    /// <summary>
    /// The host.
    /// </summary>
    private readonly string? host;

    /// <summary>
    /// The record message events flag.
    /// </summary>
    private readonly bool recordMessageEvents;

    /// <summary>
    /// The record exception as ActivityEvent flag.
    /// </summary>
    private readonly bool recordException;

    /// <summary>
    /// The RPC activity.
    /// </summary>
    private Activity? activity;

    /// <summary>
    /// The complete flag.
    /// </summary>
    private long complete;

    /// <summary>
    /// The request message counter.
    /// </summary>
    private int requestMessageCounter;

    /// <summary>
    /// The response counter.
    /// </summary>
    private int responseMessageCounter;

    /// <summary>
    /// The number of message events recorded.
    /// </summary>
    private int messageEventCounter;

    /// <summary>
    /// Whether the message event limit being exceeded has been logged (0 or 1).
    /// </summary>
    private int messageEventLimitLogged;

    /// <summary>
    /// Initializes a new instance of the <see cref="RpcScope{TRequest, TResponse}" /> class.
    /// </summary>
    /// <param name="host">The host that the current invocation will be dispatched to.</param>
    /// <param name="fullServiceName">Full name of the service.</param>
    /// <param name="recordMessageEvents">if set to <c>true</c> [record message events].</param>
    /// <param name="recordException">If set to <c>true</c> [record exception].</param>
    protected RpcScope(
        string? host,
        string? fullServiceName,
        bool recordMessageEvents,
        bool recordException)
    {
        this.host = host;
        this.FullServiceName = fullServiceName?.Trim('/') ?? "unknownservice/unknownmethod";
        this.recordMessageEvents = recordMessageEvents;
        this.recordException = recordException;
    }

    /// <summary>
    /// Gets the full name of the service.
    /// </summary>
    protected string FullServiceName { get; }

    /// <summary>
    /// Records a request message.
    /// </summary>
    /// <param name="request">The request.</param>
    public void RecordRequest(TRequest request)
    {
        Guard.ThrowIfNull(request);

        this.requestMessageCounter++;

        if (this.activity == null || !this.activity.IsAllDataRequested || !this.recordMessageEvents)
        {
            return;
        }

        if (request is IMessage message)
        {
            this.AddMessageEvent(typeof(TRequest).Name, message, request: true);
        }
    }

    /// <summary>
    /// Records a response message.
    /// </summary>
    /// <param name="response">The response.</param>
    public void RecordResponse(TResponse response)
    {
        Guard.ThrowIfNull(response);

        this.responseMessageCounter++;

        if (this.activity == null || !this.activity.IsAllDataRequested || !this.recordMessageEvents)
        {
            return;
        }

        if (response is IMessage message)
        {
            this.AddMessageEvent(typeof(TResponse).Name, message, request: false);
        }
    }

    /// <summary>
    /// Completes the RPC.
    /// </summary>
    public void Complete()
    {
        if (this.activity == null)
        {
            return;
        }

        // The overall Span status should remain unset however the grpc status code attribute is required
        this.StopActivity((int)StatusCode.OK);
    }

    /// <summary>
    /// Records a failed RPC.
    /// </summary>
    /// <param name="exception">The exception.</param>
    public void CompleteWithException(Exception exception)
    {
        Guard.ThrowIfNull(exception);

        if (this.activity == null)
        {
            return;
        }

        this.StopActivity(exception);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.activity == null)
        {
            return;
        }

        // If not already completed this will mark the Activity as cancelled.
        this.StopActivity((int)StatusCode.Cancelled);
    }

    /// <summary>
    /// Creates the tags to start the activity with, so that samplers can use them.
    /// </summary>
    /// <param name="additionalTags">The additional tags.</param>
    /// <param name="channelTarget">The channel target, used when the call has no host.</param>
    /// <returns>The tags.</returns>
    protected List<KeyValuePair<string, object?>> CreateActivityTags(
        IReadOnlyDictionary<string, object?>? additionalTags,
        string? channelTarget = null)
    {
        List<KeyValuePair<string, object?>> tags = [new(SemanticConventions.AttributeRpcSystemName, GrpcTagHelper.RpcSystemGrpc)];

        if (string.Equals(this.FullServiceName, GrpcTagHelper.GrpcMethodOther, StringComparison.Ordinal))
        {
            tags.Add(new(SemanticConventions.AttributeRpcMethod, GrpcTagHelper.RpcMethodOther));
            tags.Add(new(SemanticConventions.AttributeRpcMethodOriginal, this.FullServiceName));
        }
        else
        {
            tags.Add(new(SemanticConventions.AttributeRpcMethod, this.FullServiceName));
        }

        if (this.host is { Length: > 0 } host)
        {
            TryAddServerTags(tags, host);
        }
        else if (channelTarget is { } target && !string.IsNullOrWhiteSpace(target))
        {
            AddServerTagsFromTarget(tags, target);
        }

        if (additionalTags != null)
        {
            tags.AddRange(additionalTags);
        }

        return tags;
    }

    /// <summary>
    /// Sets the activity for this RPC scope. Should only be called once.
    /// </summary>
    /// <param name="activity">The activity.</param>
    protected void SetActivity(Activity? activity)
    {
        this.activity = activity;

        if (activity != null && string.Equals(this.FullServiceName, GrpcTagHelper.GrpcMethodOther, StringComparison.Ordinal))
        {
            activity.DisplayName = GrpcTagHelper.RpcSystemGrpc;
        }
    }

    private static bool TryAddServerTags(List<KeyValuePair<string, object?>> tags, string hostAndPort)
    {
        // A scheme with no default port keeps an explicit port such as 80.
        if (!Uri.TryCreate($"grpc://{hostAndPort}", UriKind.Absolute, out var uri) || uri.Host.Length == 0)
        {
            return false;
        }

        var host = uri.Host;
        if (uri.HostNameType == UriHostNameType.IPv6)
        {
            host = host.Substring(1, host.Length - 2);
        }

        tags.Add(new(SemanticConventions.AttributeServerAddress, host));

        if (!uri.IsDefaultPort)
        {
            tags.Add(new(SemanticConventions.AttributeServerPort, PortTelemetryHelper.GetBoxedPort(uri.Port, cacheValue: true)));
        }

        return true;
    }

    // See https://github.com/grpc/grpc/blob/v1.76.0/doc/naming.md.
    private static void AddServerTagsFromTarget(List<KeyValuePair<string, object?>> tags, string target)
    {
        var colon = target.IndexOf(':');
        var scheme = colon > 0 ? target.Substring(0, colon) : null;
        var rest = target.Substring(colon + 1);

        switch (scheme)
        {
            case "dns":
                if (rest.StartsWith("//", StringComparison.Ordinal))
                {
                    // Skip the authority, which names the DNS server.
                    var slash = rest.IndexOf('/', 2);
                    rest = slash < 0 ? string.Empty : rest.Substring(slash + 1);
                }

                if (TryAddServerTags(tags, rest))
                {
                    return;
                }

                break;

            case "unix":
                tags.Add(new(SemanticConventions.AttributeServerAddress, rest.StartsWith("//", StringComparison.Ordinal) ? rest.Substring(2) : rest));
                return;

            case "ipv4":
            case "ipv6":
                if (string.Equals(scheme, "ipv6", StringComparison.Ordinal) &&
                    !rest.StartsWith("[", StringComparison.Ordinal))
                {
                    rest = $"[{rest}]";
                }

                if (rest.IndexOf(',') < 0 && TryAddServerTags(tags, rest))
                {
                    return;
                }

                break;

            default:
                if (target.IndexOf("://", StringComparison.Ordinal) < 0 && TryAddServerTags(tags, target))
                {
                    return;
                }

                break;
        }

        tags.Add(new(SemanticConventions.AttributeServerAddress, target));
    }

    /// <summary>
    /// Stops the activity.
    /// </summary>
    /// <param name="statusCode">The status code.</param>
    /// <param name="markAsCompleted">If set to <c>true</c> [mark as completed].</param>
    private void StopActivity(int statusCode, bool markAsCompleted = true)
    {
        if ((markAsCompleted && !this.TryMarkAsCompleted()) || this.activity is null)
        {
            return;
        }

        var grpcStatusName = GrpcTagHelper.GetGrpcStatusCodeName(statusCode);
        this.activity.SetTag(SemanticConventions.AttributeRpcResponseStatusCode, grpcStatusName);

        // Resolve the span status from the gRPC status code using the rules for the span kind:
        // client spans treat every non-OK status as an error, whereas server spans only treat a
        // subset of status codes as errors.
        // See https://github.com/open-telemetry/semantic-conventions/blob/v1.42.0/docs/rpc/grpc.md
        var spanStatus = this.activity.Kind == ActivityKind.Client
            ? GrpcTagHelper.ResolveSpanStatusForGrpcStatusCodeOnClient(statusCode)
            : GrpcTagHelper.ResolveSpanStatusForGrpcStatusCodeOnServer(statusCode);

        if (spanStatus == ActivityStatusCode.Error)
        {
            this.activity.SetStatus(spanStatus);

            // error.type is conditionally required when the operation failed; for gRPC it is set to
            // the status code name.
            this.activity.SetTag(SemanticConventions.AttributeErrorType, grpcStatusName);
        }

        this.activity.Stop();
    }

    /// <summary>
    /// Stops the activity.
    /// </summary>
    /// <param name="exception">The exception.</param>
    private void StopActivity(Exception exception)
    {
        if (!this.TryMarkAsCompleted() || this.activity is null)
        {
            return;
        }

        var grpcStatusCode = exception is RpcException rpcException
            ? rpcException.StatusCode
            : StatusCode.Unknown;

        if (this.activity.IsAllDataRequested && this.recordException)
        {
            this.activity.AddException(exception);
        }

        // Defer to StopActivity to apply the span-kind specific status rules so that, for example,
        // server spans do not report an error status for status codes the conventions consider successful.
        this.StopActivity((int)grpcStatusCode, markAsCompleted: false);
    }

    /// <summary>
    /// Tries to mark <see cref="RpcScope{TRequest, TResponse}"/> as completed.
    /// </summary>
    /// <returns>Returns <c>true</c> if marked as completed successfully.</returns>
    private bool TryMarkAsCompleted()
        => Interlocked.CompareExchange(ref this.complete, 1, 0) == 0;

    /// <summary>
    /// Atomically reserves one of the message events allowed for the RPC.
    /// Requests and responses can be recorded concurrently on duplex calls.
    /// </summary>
    /// <returns>Returns <c>true</c> if an event may be recorded.</returns>
    private bool TryReserveMessageEvent()
    {
        var current = Volatile.Read(ref this.messageEventCounter);

        while (current < MaxMessageEvents)
        {
            var observed = Interlocked.CompareExchange(ref this.messageEventCounter, current + 1, current);
            if (observed == current)
            {
                return true;
            }

            current = observed;
        }

        return false;
    }

    /// <summary>
    /// Adds a message event.
    /// </summary>
    /// <param name="eventName">Name of the event.</param>
    /// <param name="message">The message.</param>
    /// <param name="request">if true this is a request message.</param>
    private void AddMessageEvent(string eventName, IMessage? message, bool request)
    {
        if (message == null || this.activity is null)
        {
            return;
        }

        if (!this.TryReserveMessageEvent())
        {
            // Only log once per call so a peer cannot also flood the log.
            if (Interlocked.Exchange(ref this.messageEventLimitLogged, 1) == 0)
            {
                GrpcCoreInstrumentationEventSource.Log.MessageEventLimitExceeded(this.FullServiceName, MaxMessageEvents);
            }

            return;
        }

        // Servers receive requests and send responses.
        var sent = request != (this.activity.Kind == ActivityKind.Server);

        var attributes = new ActivityTagsCollection(
        [
            new("name", "message"),
            new(SemanticConventions.AttributeMessageType, sent ? "SENT" : "RECEIVED"),
            new(SemanticConventions.AttributeMessageId, request ? this.requestMessageCounter : this.responseMessageCounter),

            // The compressed size is not set, because an interceptor sees the message before compression.
            new(SemanticConventions.AttributeMessageUncompressedSize, message.CalculateSize()),
        ]);

        this.activity.AddEvent(new ActivityEvent(eventName, default, attributes));
    }
}
