// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.Util;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

/// <summary>
/// Wraps the outgoing AWS SDK Request in a Span and adds additional AWS specific Tags.
/// Depending on the target AWS Service, additional request specific information may be injected as well.
/// <para />
/// This <see cref="PipelineHandler"/> must execute early in the AWS SDK pipeline
/// in order to manipulate outgoing requests objects before they are marshalled (ie serialized).
/// </summary>
internal sealed class AWSTracingPipelineHandler : PipelineHandler
{
    internal const string ActivitySourceName = "Amazon.AWS.AWSClientInstrumentation";

    private static readonly ActivitySource AWSSDKActivitySource = new(ActivitySourceName);

    private readonly AWSClientInstrumentationOptions options;

    public AWSTracingPipelineHandler(AWSClientInstrumentationOptions options)
    {
        this.options = options;
    }

    public Activity? Activity { get; private set; }

    public override void InvokeSync(IExecutionContext executionContext)
    {
        this.Activity = this.ProcessBeginRequest(executionContext);
        try
        {
            base.InvokeSync(executionContext);
        }
        catch (Exception ex)
        {
            if (this.Activity != null)
            {
                ProcessException(this.Activity, ex);
            }

            throw;
        }
        finally
        {
            if (this.Activity != null)
            {
                ProcessEndRequest(executionContext, this.Activity);
            }
        }
    }

    public override async Task<T> InvokeAsync<T>(IExecutionContext executionContext)
    {
        T? ret = null;

        this.Activity = this.ProcessBeginRequest(executionContext);
        try
        {
            ret = await base.InvokeAsync<T>(executionContext).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (this.Activity != null)
            {
                ProcessException(this.Activity, ex);
            }

            throw;
        }
        finally
        {
            if (this.Activity != null)
            {
                ProcessEndRequest(executionContext, this.Activity);
            }
        }

        return ret;
    }

    private static void ProcessEndRequest(IExecutionContext executionContext, Activity activity)
    {
        var responseContext = executionContext.ResponseContext;
        var requestContext = executionContext.RequestContext;

        if (activity.IsAllDataRequested)
        {
            if (Utils.GetTagValue(activity, AWSSemanticConventions.AttributeAWSRequestId) == null)
            {
                activity.SetTag(AWSSemanticConventions.AttributeAWSRequestId, FetchRequestId(requestContext, responseContext));
            }

            var httpResponse = responseContext.HttpResponse;
            if (httpResponse != null)
            {
                int statusCode = (int)httpResponse.StatusCode;

                AddStatusCodeToActivity(activity, statusCode);
                activity.SetTag(AWSSemanticConventions.AttributeHttpResponseContentLength, httpResponse.ContentLength);
            }
        }

        activity.Stop();
    }

    private static void ProcessException(Activity activity, Exception ex)
    {
        if (activity.IsAllDataRequested)
        {
            activity.RecordException(ex);

            activity.SetStatus(Status.Error.WithDescription(ex.Message));

            if (ex is AmazonServiceException amazonServiceException)
            {
                AddStatusCodeToActivity(activity, (int)amazonServiceException.StatusCode);
                activity.SetTag(AWSSemanticConventions.AttributeAWSRequestId, amazonServiceException.RequestId);
            }
        }
    }

#if NET6_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2075",
        Justification = "The reflected properties were already used by the AWS SDK's marshallers so the properties could not have been trimmed.")]
#endif
    private static void AddRequestSpecificInformation(Activity activity, IRequestContext requestContext, string service)
    {
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

    private static void AddStatusCodeToActivity(Activity activity, int status_code)
    {
        activity.SetTag(AWSSemanticConventions.AttributeHttpStatusCode, status_code);
    }

    private static string FetchRequestId(IRequestContext requestContext, IResponseContext responseContext)
    {
        string request_id = string.Empty;
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

    private Activity? ProcessBeginRequest(IExecutionContext executionContext)
    {
        var requestContext = executionContext.RequestContext;
        var service = AWSServiceHelper.GetAWSServiceName(requestContext);
        var operation = AWSServiceHelper.GetAWSOperationName(requestContext);

        Activity? activity = AWSSDKActivitySource.StartActivity(service + "." + operation, ActivityKind.Client);

        if (activity == null)
        {
            return null;
        }

        if (this.options.SuppressDownstreamInstrumentation)
        {
            SuppressInstrumentationScope.Enter();
        }

        if (activity.IsAllDataRequested)
        {
            activity.SetTag(AWSSemanticConventions.AttributeAWSServiceName, service);
            activity.SetTag(AWSSemanticConventions.AttributeAWSOperationName, operation);
            var client = executionContext.RequestContext.ClientConfig;
            if (client != null)
            {
                var region = client.RegionEndpoint?.SystemName;
                activity.SetTag(AWSSemanticConventions.AttributeAWSRegion, region ?? AWSSDKUtils.DetermineRegion(client.ServiceURL));
            }

            AddRequestSpecificInformation(activity, requestContext, service);
        }

        return activity;
    }
}
