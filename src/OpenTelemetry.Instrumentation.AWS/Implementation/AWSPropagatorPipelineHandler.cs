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

        ProcessEndRequest(executionContext);
    }

    public override async Task<T> InvokeAsync<T>(IExecutionContext executionContext)
    {
        T? ret = null;

        this.ProcessBeginRequest(executionContext);

        ret = await base.InvokeAsync<T>(executionContext).ConfigureAwait(false);

        ProcessEndRequest(executionContext);

        return ret;
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

        if (AWSServiceHelper.ServiceRequestParameterMap.TryGetValue(service, out var parameters))
        {
            AmazonWebServiceRequest request = requestContext.OriginalRequest;

            foreach (var parameter in parameters)
            {
                try
                {
                    // for bedrock agent, we only extract one attribute based on the operation.
                    if (AWSServiceType.IsBedrockAgentService(service))
                    {
                        if (AWSServiceHelper.OperationNameToResourceMap()[AWSServiceHelper.GetAWSOperationName(requestContext)] != parameter)
                        {
                            continue;
                        }
                    }

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
        else if (AWSServiceType.IsBedrockRuntimeService(service))
        {
            activity.SetTag(AWSSemanticConventions.AttributeGenAiSystem, "aws_bedrock");
        }
    }

#if NET
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2075",
        Justification = "The reflected properties were already used by the AWS SDK's marshallers so the properties could not have been trimmed.")]
#endif
    private static void AddResponseSpecificInformation(Activity activity, IExecutionContext executionContext)
    {
        var service = executionContext.RequestContext.ServiceMetaData.ServiceId;
        var responseContext = executionContext.ResponseContext;

        if (AWSServiceHelper.ServiceResponseParameterMap.TryGetValue(service, out var parameters))
        {
            AmazonWebServiceResponse response = responseContext.Response;

            foreach (var parameter in parameters)
            {
                try
                {
                    // for bedrock agent, extract attribute from object in response.
                    if (AWSServiceType.IsBedrockAgentService(service))
                    {
                        var operationName = Utils.RemoveSuffix(response.GetType().Name, "Response");
                        if (AWSServiceHelper.OperationNameToResourceMap()[operationName] == parameter)
                        {
                            AddBedrockAgentResponseAttribute(activity, response, parameter);
                        }
                    }

                    var property = response.GetType().GetProperty(parameter);
                    if (property != null)
                    {
                        if (AWSServiceHelper.ParameterAttributeMap.TryGetValue(parameter, out var attribute))
                        {
                            activity.SetTag(attribute, property.GetValue(response));
                        }
                    }
                }
                catch (Exception)
                {
                    // Guard against any reflection-related exceptions when running in AoT.
                    // See https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/1543#issuecomment-1907667722.
                }
            }
        }
    }

#if NET
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2075",
        Justification = "The reflected properties were already used by the AWS SDK's marshallers so the properties could not have been trimmed.")]
#endif
    private static void AddBedrockAgentResponseAttribute(Activity activity, AmazonWebServiceResponse response, string parameter)
    {
        var responseObject = response.GetType().GetProperty(Utils.RemoveSuffix(parameter, "Id"));
        if (responseObject != null)
        {
            var attributeObject = responseObject.GetValue(response);
            if (attributeObject != null)
            {
                var property = attributeObject.GetType().GetProperty(parameter);
                if (property != null)
                {
                    if (AWSServiceHelper.ParameterAttributeMap.TryGetValue(parameter, out var attribute))
                    {
                        activity.SetTag(attribute, property.GetValue(attributeObject));
                    }
                }
            }
        }
    }

    private static void ProcessEndRequest(IExecutionContext executionContext)
    {
        var currentActivity = Activity.Current;

        if (currentActivity == null || !currentActivity.Source.Name.StartsWith(TelemetryConstants.TelemetryScopePrefix, StringComparison.Ordinal))
        {
            return;
        }

        AddResponseSpecificInformation(currentActivity, executionContext);
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
