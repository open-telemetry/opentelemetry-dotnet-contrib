// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.SimpleNotificationService;
using Amazon.SQS;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

/// <summary>
/// Wires <see cref="AWSTracingPipelineHandler"/> and <see cref="AWSPropagatorPipelineHandler"/> into the AWS
/// <see cref="RuntimePipeline"/> so they can inject trace headers and add request information to the tags.
/// </summary>
internal class AWSTracingPipelineCustomizer : IRuntimePipelineCustomizer
{
    public const string UniqueName = "AWS Tracing Registration Customization";

    private readonly AWSClientInstrumentationOptions options;

    public AWSTracingPipelineCustomizer(AWSClientInstrumentationOptions options)
    {
        this.options = options;
    }

    string IRuntimePipelineCustomizer.UniqueName => UniqueName;

    public void Customize(Type type, RuntimePipeline pipeline)
    {
        if (!typeof(AmazonServiceClient).IsAssignableFrom(type))
        {
            return;
        }

        if (AWSServiceHelper.UsesProducerSpansForMessageSend(this.options.SemanticConventionVersion) && IsMessagingClient(type))
        {
            // MetricsHandler creates the SDK span, so the message's creation context must be available before it runs.
            pipeline.AddHandlerBefore<MetricsHandler>(new AWSMessageContextPipelineHandler());
        }

        var tracingPipelineHandler = new AWSTracingPipelineHandler(this.options);
        var propagatingPipelineHandler = new AWSPropagatorPipelineHandler();

        // AWSTracingPipelineHandler must execute early in the AWS SDK pipeline
        // in order to manipulate outgoing requests objects before they are marshalled (ie serialized).
        pipeline.AddHandlerBefore<Marshaller>(tracingPipelineHandler);

        // AWSPropagatorPipelineHandler executes after the AWS SDK has marshalled (ie serialized)
        // the outgoing request object so that it can work with the request's Headers
        pipeline.AddHandlerBefore<RetryHandler>(propagatingPipelineHandler);
    }

    internal static bool IsMessagingClient(Type type)
        => typeof(AmazonSQSClient).IsAssignableFrom(type) ||
           typeof(AmazonSimpleNotificationServiceClient).IsAssignableFrom(type);
}
