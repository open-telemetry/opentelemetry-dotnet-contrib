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
/// Makes an existing message creation context available before the AWS SDK creates its send span.
/// </summary>
internal sealed class AWSMessageContextPipelineHandler : PipelineHandler
{
    private static readonly AsyncLocal<ActivityContext> CreationContextSlot = new();

    internal static ActivityContext CreationContext => CreationContextSlot.Value;

    public override void InvokeSync(IExecutionContext executionContext)
    {
        var previousContext = CreationContextSlot.Value;

        try
        {
            CreationContextSlot.Value = ExtractCreationContext(executionContext.RequestContext.OriginalRequest);
            base.InvokeSync(executionContext);
        }
        finally
        {
            CreationContextSlot.Value = previousContext;
        }
    }

    public override async Task<T> InvokeAsync<T>(IExecutionContext executionContext)
    {
        var previousContext = CreationContextSlot.Value;

        try
        {
            CreationContextSlot.Value = ExtractCreationContext(executionContext.RequestContext.OriginalRequest);
            return await base.InvokeAsync<T>(executionContext).ConfigureAwait(false);
        }
        finally
        {
            CreationContextSlot.Value = previousContext;
        }
    }

    private static ActivityContext ExtractCreationContext(AmazonWebServiceRequest request)
        => request switch
        {
            SQS.SendMessageRequest { MessageAttributes: { } attributes } => Propagators.DefaultTextMapPropagator.Extract(
                default,
                attributes,
                static (carrier, key) => carrier.TryGetValue(key, out var value) ? [value.StringValue] : []).ActivityContext,
            SNS.PublishRequest { MessageAttributes: { } attributes } => Propagators.DefaultTextMapPropagator.Extract(
                default,
                attributes,
                static (carrier, key) => carrier.TryGetValue(key, out var value) ? [value.StringValue] : []).ActivityContext,
            _ => default,
        };
}
