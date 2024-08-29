// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.Runtime.Telemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Extensions.AWS.Trace;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

/// <summary>
/// Uses <see cref="AWSXRayPropagator"/> to inject the current Activity Context and
/// Baggage into the outgoing AWS SDK Request.
/// <para />
/// Must execute after the AWS SDK has marshalled (ie serialized)
/// the outgoing request object so that it can work with the <see cref="IRequest"/>'s
/// <see cref="IRequest.Headers"/>.
/// </summary>
internal class AWSPropagatorPipelineHandler : PipelineHandler
{
    private static readonly AWSXRayPropagator AwsPropagator = new();

    private static readonly Action<IDictionary<string, string>, string, string> Setter = (carrier, name, value) =>
    {
        carrier[name] = value;
    };

    private readonly AWSClientInstrumentationOptions options;

    public AWSPropagatorPipelineHandler(AWSClientInstrumentationOptions options)
    {
        this.options = options;
    }

    public override void InvokeSync(IExecutionContext executionContext)
    {
        this.ProcessBeginRequest(executionContext);

        base.InvokeSync(executionContext);
    }

    public override async Task<T> InvokeAsync<T>(IExecutionContext executionContext)
    {
        this.ProcessBeginRequest(executionContext);

        return await base.InvokeAsync<T>(executionContext).ConfigureAwait(false);
    }

#if NET
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2075",
        Justification = "The reflected properties were already used by the AWS SDK's marshallers so the properties could not have been trimmed.")]
#endif
    private static void AddRequestSpecificInformation(Activity activity, IRequestContext requestContext)
    {
        var service = requestContext.ServiceMetaData.ServiceId;

        if (AWSServiceHelper.ServiceParameterMap.TryGetValue(service, out var parameter))
        {
            AmazonWebServiceRequest request = requestContext.OriginalRequest;

            try
            {
                var property = request.GetType().GetProperty(parameter);
                if (property != null)
                {
                    if (AWSServiceHelper.ParameterAttributeMap.TryGetValue(parameter, out var attribute))
                    {
                        activity.SetTag(attribute, property.GetValue(request));
                    }
                }
            }
            catch (Exception)
            {
                // Guard against any reflection-related exceptions when running in AoT.
                // See https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/1543#issuecomment-1907667722.
            }
        }

        if (AWSServiceType.IsDynamoDbService(service))
        {
            activity.SetTag(SemanticConventions.AttributeDbSystem, AWSSemanticConventions.AttributeValueDynamoDb);
        }
        else if (AWSServiceType.IsSqsService(service))
        {
            SqsRequestContextHelper.AddAttributes(
                requestContext, AWSMessagingUtils.InjectIntoDictionary(new PropagationContext(activity.Context, Baggage.Current)));
        }
        else if (AWSServiceType.IsSnsService(service))
        {
            SnsRequestContextHelper.AddAttributes(
                requestContext, AWSMessagingUtils.InjectIntoDictionary(new PropagationContext(activity.Context, Baggage.Current)));
        }
    }

    private void ProcessBeginRequest(IExecutionContext executionContext)
    {
        if (this.options.SuppressDownstreamInstrumentation)
        {
            SuppressInstrumentationScope.Enter();
        }

        var currentActivity = Activity.Current;

        // Propagate the current activity if it was created by the AWS SDK
        if (currentActivity == null || !currentActivity.Source.Name.StartsWith(TelemetryConstants.TelemetryScopePrefix, StringComparison.Ordinal))
        {
            return;
        }

        AddRequestSpecificInformation(currentActivity, executionContext.RequestContext);
        AwsPropagator.Inject(
            new PropagationContext(currentActivity.Context, Baggage.Current),
            executionContext.RequestContext.Request.Headers,
            Setter);
    }
}
