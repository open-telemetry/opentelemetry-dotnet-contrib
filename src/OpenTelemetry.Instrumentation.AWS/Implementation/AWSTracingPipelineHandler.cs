// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.Runtime.Telemetry;
using Amazon.Util;
using OpenTelemetry.AWS;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

/// <summary>
/// Adds additional request and response tags depending on the target AWS Service.
/// <para />
/// This <see cref="PipelineHandler"/> must execute early in the AWS SDK pipeline
/// in order to manipulate outgoing requests objects before they are marshalled (ie serialized).
/// </summary>
internal sealed class AWSTracingPipelineHandler : PipelineHandler
{
    private readonly AWSClientInstrumentationOptions options;
    private readonly AWSSemanticConventions awsSemanticConventions;
    private readonly AWSServiceHelper awsServiceHelper;

    public AWSTracingPipelineHandler(AWSClientInstrumentationOptions options)
    {
        this.options = options;
        this.awsSemanticConventions = new AWSSemanticConventions(options.SemanticConventionVersion);
        this.awsServiceHelper = new AWSServiceHelper(this.awsSemanticConventions);
    }

    public override void InvokeSync(IExecutionContext executionContext)
    {
        var activity = this.ProcessBeginRequest(executionContext);
        base.InvokeSync(executionContext);
        this.ProcessEndRequest(activity, executionContext);
    }

    public override async Task<T> InvokeAsync<T>(IExecutionContext executionContext)
    {
        var activity = this.ProcessBeginRequest(executionContext);
        var ret = await base.InvokeAsync<T>(executionContext).ConfigureAwait(false);

        this.ProcessEndRequest(activity, executionContext);

        return ret;
    }

    private static string FetchRequestId(IRequestContext requestContext, IResponseContext responseContext)
    {
        var request_id = string.Empty;
        var response = responseContext.Response;
        if (response != null)
        {
            request_id = response.ResponseMetadata.RequestId;
        }
        else
        {
            var request_headers = requestContext.Request.Headers;
            if (string.IsNullOrEmpty(request_id) && request_headers.TryGetValue("x-amzn-RequestId", out var req_id))
            {
                request_id = req_id;
            }

            if (string.IsNullOrEmpty(request_id) && request_headers.TryGetValue("x-amz-request-id", out req_id))
            {
                request_id = req_id;
            }

            if (string.IsNullOrEmpty(request_id) && request_headers.TryGetValue("x-amz-id-2", out req_id))
            {
                request_id = req_id;
            }
        }

        return request_id;
    }

    private static void AddPropagationDataToRequest(Activity activity, IRequestContext requestContext)
    {
        var service = requestContext.ServiceMetaData.ServiceId;

        if (AWSServiceType.IsSqsService(service))
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

#if NET
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2075",
        Justification = "The reflected properties were already used by the AWS SDK's marshallers so the properties could not have been trimmed.")]
#endif
    private void AddResponseSpecificInformation(Activity activity, IExecutionContext executionContext)
    {
        var service = executionContext.RequestContext.ServiceMetaData.ServiceId;
        var responseContext = executionContext.ResponseContext;

        if (AWSServiceHelper.ServiceResponseParameterMap.TryGetValue(service, out var parameters))
        {
            var response = responseContext.Response;

            foreach (var parameter in parameters)
            {
                try
                {
                    var property = response.GetType().GetProperty(parameter);
                    if (property != null)
                    {
                        if (this.awsServiceHelper.ParameterAttributeMap.TryGetValue(parameter, out var attribute))
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

        // for bedrock runtime, LLM specific attributes are extracted based on the model ID.
        if (AWSServiceType.IsBedrockRuntimeService(service))
        {
            var model = this.awsSemanticConventions.TagExtractor.GetTagAttributeGenAiModelId(activity);
            if (model != null)
            {
                var modelString = model.ToString();
                if (modelString != null)
                {
                    AWSLlmModelProcessor.ProcessGenAiAttributes(activity, responseContext.Response, modelString, false, this.awsSemanticConventions);
                }
            }
        }

        var httpResponse = responseContext.HttpResponse;
        if (httpResponse != null)
        {
            var statusCode = (int)httpResponse.StatusCode;

            this.AddStatusCodeToActivity(activity, statusCode);
            this.awsSemanticConventions.TagBuilder.SetTagAttributeHttpResponseContentLength(activity, httpResponse.ContentLength);
        }
    }

#if NET
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2075",
        Justification = "The reflected properties were already used by the AWS SDK's marshallers so the properties could not have been trimmed.")]
#endif
    private void AddRequestSpecificInformation(Activity activity, IRequestContext requestContext)
    {
        var service = AWSServiceHelper.GetAWSServiceName(requestContext);

        if (AWSServiceHelper.ServiceRequestParameterMap.TryGetValue(service, out var parameters))
        {
            var request = requestContext.OriginalRequest;

            foreach (var parameter in parameters)
            {
                try
                {
                    var property = request.GetType().GetProperty(parameter);
                    if (property != null)
                    {
                        // for bedrock runtime, LLM specific attributes are extracted based on the model ID.
                        if (AWSServiceType.IsBedrockRuntimeService(service) && parameter == "ModelId")
                        {
                            var model = property.GetValue(request);
                            if (model != null)
                            {
                                var modelString = model.ToString();
                                if (modelString != null)
                                {
                                    AWSLlmModelProcessor.ProcessGenAiAttributes(activity, request, modelString, true, this.awsSemanticConventions);
                                }
                            }
                        }

                        // for secrets manager, only extract SecretId from request if it is a secret ARN.
                        if (AWSServiceType.IsSecretsManagerService(service) && parameter == "SecretId")
                        {
                            var secretId = property.GetValue(request);
                            if (secretId != null)
                            {
                                var secretIdString = secretId.ToString();
                                if (secretIdString != null && !secretIdString.StartsWith("arn:aws:secretsmanager:", StringComparison.Ordinal))
                                {
                                    continue;
                                }
                            }
                        }

                        if (this.awsServiceHelper.ParameterAttributeMap.TryGetValue(parameter, out var attribute))
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
            this.awsSemanticConventions.TagBuilder.SetTagAttributeDbSystemToDynamoDb(activity);
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
            this.awsSemanticConventions.TagBuilder.SetTagAttributeGenAiSystemToBedrock(activity);
        }

        var client = requestContext.ClientConfig;
        if (client != null)
        {
            var region = client.RegionEndpoint?.SystemName;
            this.awsSemanticConventions.TagBuilder.SetTagAttributeAWSRegion(activity, region ?? AWSSDKUtils.DetermineRegion(client.ServiceURL));
        }
    }

    private void ProcessEndRequest(Activity? activity, IExecutionContext executionContext)
    {
        if (activity == null || !activity.IsAllDataRequested)
        {
            return;
        }

        var responseContext = executionContext.ResponseContext;
        var requestContext = executionContext.RequestContext;
        if (this.awsSemanticConventions.TagExtractor.GetTagAttributeAWSRequestId == null)
        {
            this.awsSemanticConventions.TagBuilder.SetTagAttributeAWSRequestId(activity, FetchRequestId(requestContext, responseContext));
        }

        this.AddResponseSpecificInformation(activity, executionContext);
    }

    private Activity? ProcessBeginRequest(IExecutionContext executionContext)
    {
        if (this.options.SuppressDownstreamInstrumentation)
        {
            SuppressInstrumentationScope.Enter();
        }

        var currentActivity = Activity.Current;

        if (currentActivity == null)
        {
            return null;
        }

        if (currentActivity.IsAllDataRequested
            && currentActivity.Source.Name.StartsWith(TelemetryConstants.TelemetryScopePrefix, StringComparison.Ordinal))
        {
            this.AddRequestSpecificInformation(currentActivity, executionContext.RequestContext);
        }

        // Context propagation should always happen regardless of sampling decision (which affects Activity.IsAllDataRequested and Activity.Source).
        // Otherwise, downstream services can make inconsistent sampling decisions and create incomplete traces.
        AddPropagationDataToRequest(currentActivity, executionContext.RequestContext);

        return currentActivity;
    }

    private void AddStatusCodeToActivity(Activity activity, int status_code)
    {
        this.awsSemanticConventions.TagBuilder.SetTagAttributeHttpResponseStatusCode(activity, status_code);
    }
}
