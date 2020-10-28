﻿// <copyright file="AWSTracingPipelineHandler.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.Util;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Instrumentation.AWS
{
    internal class AWSTracingPipelineHandler : PipelineHandler
    {
        internal const string ActivitySourceName = "Amazon.AWS.AWSClientInstrumentation";

        private static readonly AWSXRayPropagator AwsPropagator = new AWSXRayPropagator();
        private static readonly Action<IDictionary<string, string>, string, string> Setter = (carrier, name, value) =>
        {
            carrier[name] = value;
        };

        private static readonly Dictionary<string, string> ServiceParameterMap = new Dictionary<string, string>()
        {
            { "DynamoDBv2", "TableName" },
            { "SQS", "QueueUrl" },
        };

        private static readonly Dictionary<string, string> ParameterAttributeMap = new Dictionary<string, string>()
        {
            { "TableName", AWSSemanticConventions.AttributeAWSDynamoTableName },
            { "QueueUrl", AWSSemanticConventions.AttributeAWSSQSQueueUrl },
        };

        private static readonly ActivitySource AWSSDKActivitySource = new ActivitySource(ActivitySourceName);

        public override void InvokeSync(IExecutionContext executionContext)
        {
            var activity = this.ProcessBeginRequest(executionContext);
            try
            {
                base.InvokeSync(executionContext);
            }
            catch (Exception ex)
            {
                if (activity != null)
                {
                    this.ProcessException(activity, ex);
                }

                throw;
            }
            finally
            {
                if (activity != null)
                {
                    this.ProcessEndRequest(executionContext, activity);
                }
            }
        }

        public override async Task<T> InvokeAsync<T>(IExecutionContext executionContext)
        {
            T ret = null;

            var activity = this.ProcessBeginRequest(executionContext);
            try
            {
                ret = await base.InvokeAsync<T>(executionContext).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (activity != null)
                {
                    this.ProcessException(activity, ex);
                }

                throw;
            }
            finally
            {
                if (activity != null)
                {
                    this.ProcessEndRequest(executionContext, activity);
                }
            }

            return ret;
        }

        private Activity ProcessBeginRequest(IExecutionContext executionContext)
        {
            Activity activity = null;

            var requestContext = executionContext.RequestContext;
            var service = this.GetAWSServiceName(requestContext);
            var operation = this.GetAWSOperationName(requestContext);

            activity = AWSSDKActivitySource.StartActivity(service + "." + operation, ActivityKind.Client);

            if (activity == null)
            {
                return null;
            }

            activity.SetTag(AWSSemanticConventions.AttributeAWSServiceName, service);
            activity.SetTag(AWSSemanticConventions.AttributeAWSOperationName, operation);
            var client = executionContext.RequestContext.ClientConfig;
            if (client != null)
            {
                var region = client.RegionEndpoint?.SystemName;
                activity.SetTag(AWSSemanticConventions.AttributeAWSRegion, region ?? AWSSDKUtils.DetermineRegion(client.ServiceURL));
            }

            this.AddRequestSpecificInformation(activity, requestContext, service);

            AwsPropagator.Inject(new PropagationContext(activity.Context, Baggage.Current), requestContext.Request.Headers, Setter);

            return activity;
        }

        private void ProcessEndRequest(IExecutionContext executionContext, Activity activity)
        {
            var responseContext = executionContext.ResponseContext;
            var requestContext = executionContext.RequestContext;

            if (activity.GetTagValue(AWSSemanticConventions.AttributeAWSRequestId) == null)
            {
                activity.SetTag(AWSSemanticConventions.AttributeAWSRequestId, this.FetchRequestId(requestContext, responseContext));
            }

            var httpResponse = responseContext.HttpResponse;
            if (httpResponse != null)
            {
                int statusCode = (int)httpResponse.StatusCode;

                var status = activity.GetStatus();

                if (!status.Equals(Status.Error) && statusCode >= 100 && statusCode <= 399)
                {
                    activity.SetStatus(Status.Unset);
                }

                this.AddStatusCodeToActivity(activity, statusCode);
                activity.SetTag(AWSSemanticConventions.AttributeHttpResponseContentLength, httpResponse.ContentLength);
            }

            activity.Stop();
        }

        private void ProcessException(Activity activity, Exception ex)
        {
            activity.RecordException(ex);

            activity.SetStatus(Status.Error.WithDescription(ex.Message));

            if (ex is AmazonServiceException amazonServiceException)
            {
                this.AddStatusCodeToActivity(activity, (int)amazonServiceException.StatusCode);
                activity.SetTag(AWSSemanticConventions.AttributeAWSRequestId, amazonServiceException.RequestId);
            }
        }

        private string GetAWSServiceName(IRequestContext requestContext)
        {
            string serviceName = string.Empty;
            serviceName = Utils.RemoveAmazonPrefixFromServiceName(requestContext.Request.ServiceName);
            return serviceName;
        }

        private string GetAWSOperationName(IRequestContext requestContext)
        {
            string operationName = string.Empty;
            string completeRequestName = requestContext.OriginalRequest.GetType().Name;
            string suffix = "Request";
            operationName = Utils.RemoveSuffix(completeRequestName, suffix);
            return operationName;
        }

        private void AddRequestSpecificInformation(Activity activity, IRequestContext requestContext, string service)
        {
            AmazonWebServiceRequest request = requestContext.OriginalRequest;

            if (ServiceParameterMap.TryGetValue(service, out string parameter))
            {
                var property = request.GetType().GetProperty(parameter);
                if (property != null)
                {
                    if (ParameterAttributeMap.TryGetValue(parameter, out string attribute))
                    {
                        activity.SetTag(attribute, property.GetValue(request));
                    }
                }
            }
        }

        private void AddStatusCodeToActivity(Activity activity, int status_code)
        {
            activity.SetTag(AWSSemanticConventions.AttributeHttpStatusCode, status_code);
        }

        private string FetchRequestId(IRequestContext requestContext, IResponseContext responseContext)
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
                if (string.IsNullOrEmpty(request_id) && request_headers.TryGetValue("x-amzn-RequestId", out string req_id))
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
    }
}
