// <copyright file="RpcScope.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Google.Protobuf;
using Grpc.Core;
using OpenTelemetry.Trace;

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
    /// The record message events flag.
    /// </summary>
    private readonly bool recordMessageEvents;

    /// <summary>
    /// The RPC activity.
    /// </summary>
    private Activity activity;

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
    /// Initializes a new instance of the <see cref="RpcScope{TRequest, TResponse}" /> class.
    /// </summary>
    /// <param name="fullServiceName">Full name of the service.</param>
    /// <param name="recordMessageEvents">if set to <c>true</c> [record message events].</param>
    protected RpcScope(string fullServiceName, bool recordMessageEvents)
    {
        this.FullServiceName = fullServiceName?.TrimStart('/') ?? "unknownservice/unknownmethod";
        this.recordMessageEvents = recordMessageEvents;
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
        this.requestMessageCounter++;

        if (this.activity == null || !this.activity.IsAllDataRequested || !this.recordMessageEvents)
        {
            return;
        }

        this.AddMessageEvent(typeof(TRequest).Name, request as IMessage, request: true);
    }

    /// <summary>
    /// Records a response message.
    /// </summary>
    /// <param name="response">The response.</param>
    public void RecordResponse(TResponse response)
    {
        this.responseMessageCounter++;

        if (this.activity == null || !this.activity.IsAllDataRequested || !this.recordMessageEvents)
        {
            return;
        }

        this.AddMessageEvent(typeof(TResponse).Name, response as IMessage, request: false);
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
        this.StopActivity((int)Grpc.Core.StatusCode.OK);
    }

    /// <summary>
    /// Records a failed RPC.
    /// </summary>
    /// <param name="exception">The exception.</param>
    public void CompleteWithException(Exception exception)
    {
        if (this.activity == null)
        {
            return;
        }

        var grpcStatusCode = Grpc.Core.StatusCode.Unknown;
        var description = exception.Message;

        if (exception is RpcException rpcException)
        {
            grpcStatusCode = rpcException.StatusCode;
            description = rpcException.Message;
        }

        this.StopActivity((int)grpcStatusCode, description);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.activity == null)
        {
            return;
        }

        // If not already completed this will mark the Activity as cancelled.
        this.StopActivity((int)Grpc.Core.StatusCode.Cancelled);
    }

    /// <summary>
    /// Sets the activity for this RPC scope. Should only be called once.
    /// </summary>
    /// <param name="activity">The activity.</param>
    protected void SetActivity(Activity activity)
    {
        this.activity = activity;

        if (this.activity == null || !this.activity.IsAllDataRequested)
        {
            return;
        }

        // assign some reasonable defaults
        var rpcService = this.FullServiceName;
        var rpcMethod = this.FullServiceName;

        // split the full service name by the slash
        var parts = this.FullServiceName.Split('/');
        if (parts.Length == 2)
        {
            rpcService = parts[0];
            rpcMethod = parts[1];
        }

        this.activity.SetTag(SemanticConventions.AttributeRpcSystem, "grpc");
        this.activity.SetTag(SemanticConventions.AttributeRpcService, rpcService);
        this.activity.SetTag(SemanticConventions.AttributeRpcMethod, rpcMethod);
    }

    /// <summary>
    /// Stops the activity.
    /// </summary>
    /// <param name="statusCode">The status code.</param>
    /// <param name="statusDescription">The description, if any.</param>
    private void StopActivity(int statusCode, string statusDescription = null)
    {
        if (Interlocked.CompareExchange(ref this.complete, 1, 0) == 0)
        {
            this.activity.SetTag(SemanticConventions.AttributeRpcGrpcStatusCode, statusCode);
            if (statusDescription != null)
            {
                this.activity.SetStatus(Trace.Status.Error.WithDescription(statusDescription));
            }

            this.activity.Stop();
        }
    }

    /// <summary>
    /// Adds a message event.
    /// </summary>
    /// <param name="eventName">Name of the event.</param>
    /// <param name="message">The message.</param>
    /// <param name="request">if true this is a request message.</param>
    private void AddMessageEvent(string eventName, IMessage message, bool request)
    {
        var messageSize = message.CalculateSize();

        var attributes = new ActivityTagsCollection(new KeyValuePair<string, object>[5]
        {
            new KeyValuePair<string, object>("name", "message"),
            new KeyValuePair<string, object>(SemanticConventions.AttributeMessageType, request ? "SENT" : "RECEIVED"),
            new KeyValuePair<string, object>(SemanticConventions.AttributeMessageID, request ? this.requestMessageCounter : this.responseMessageCounter),

            // TODO how to get the real compressed or uncompressed sizes
            new KeyValuePair<string, object>(SemanticConventions.AttributeMessageCompressedSize, messageSize),
            new KeyValuePair<string, object>(SemanticConventions.AttributeMessageUncompressedSize, messageSize),
        });

        this.activity.AddEvent(new ActivityEvent(eventName, default, attributes));
    }
}
