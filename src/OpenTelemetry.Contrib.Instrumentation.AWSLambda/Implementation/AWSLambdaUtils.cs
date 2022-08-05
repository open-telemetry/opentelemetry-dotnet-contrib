// <copyright file="AWSLambdaUtils.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;

namespace OpenTelemetry.Contrib.Instrumentation.AWSLambda.Implementation
{
    /// <summary>
    /// Class for getting AWS Lambda related attributes.
    /// </summary>
    internal static class AWSLambdaUtils
    {
        internal const string ActivitySourceName = "Amazon.AWS.AWSLambdaInstrumentation";
        private const string CloudProvider = "aws";
        private const string AWSRegion = "AWS_REGION";
        private const string AWSXRayLambdaTraceHeaderKey = "_X_AMZN_TRACE_ID";
        private const string AWSXRayTraceHeaderKey = "X-Amzn-Trace-Id";
        private const string FunctionName = "AWS_LAMBDA_FUNCTION_NAME";
        private const string FunctionVersion = "AWS_LAMBDA_FUNCTION_VERSION";

        private static readonly Func<IDictionary<string, string>, string, IEnumerable<string>> Getter = (headers, name) =>
        {
            if (headers.TryGetValue(name, out var value))
            {
                return new[] { value };
            }

            return new string[0];
        };

        internal static ActivityContext GetXRayParentContext()
        {
            // Currently get trace header from Lambda runtime environment variable
            // https://docs.aws.amazon.com/lambda/latest/dg/configuration-envvars.html#configuration-envvars-runtime
            // TODO: Add steps to extract trace header from http header

            var tracerHeaderValue = Environment.GetEnvironmentVariable(AWSXRayLambdaTraceHeaderKey);
            if (tracerHeaderValue == null)
            {
                return default;
            }

            var activityContext = ParseXRayTraceHeader(tracerHeaderValue);
            return activityContext;
        }

        internal static ActivityContext ExtractParentContext<TInput>(TInput input)
        {
            PropagationContext propagationContext = default;
            switch (input)
            {
                case APIGatewayProxyRequest apiGatewayProxyRequest:
                    propagationContext = Propagators.DefaultTextMapPropagator.Extract(default, apiGatewayProxyRequest, GetHeaderValues);
                    break;
                case APIGatewayHttpApiV2ProxyRequest apiGatewayHttpApiV2ProxyRequest:
                    propagationContext = Propagators.DefaultTextMapPropagator.Extract(default, apiGatewayHttpApiV2ProxyRequest, GetHeaderValues);
                    break;
            }

            return propagationContext.ActivityContext;
        }

        internal static string GetCloudProvider()
        {
            return CloudProvider;
        }

        internal static string GetAWSRegion()
        {
            return Environment.GetEnvironmentVariable(AWSRegion);
        }

        internal static string GetFunctionName(ILambdaContext context = null)
        {
            return context?.FunctionName ?? Environment.GetEnvironmentVariable(FunctionName);
        }

        internal static string GetFunctionVersion()
        {
            return Environment.GetEnvironmentVariable(FunctionVersion);
        }

        internal static IEnumerable<KeyValuePair<string, object>> GetFunctionTags<TInput>(TInput input, ILambdaContext context)
        {
            var tags = new List<KeyValuePair<string, object>>
            {
                new(AWSLambdaSemanticConventions.AttributeFaasTrigger, GetFaasTrigger(input)),
            };

            var functionName = GetFunctionName(context);
            if (functionName != null)
            {
                tags.Add(new(AWSLambdaSemanticConventions.AttributeFaasName, functionName));
            }

            if (context == null)
            {
                return tags;
            }

            if (context.AwsRequestId != null)
            {
                tags.Add(new(AWSLambdaSemanticConventions.AttributeFaasExecution, context.AwsRequestId));
            }

            var functionArn = context.InvokedFunctionArn;
            if (functionArn != null)
            {
                tags.Add(new(AWSLambdaSemanticConventions.AttributeFaasID, GetFaasId(functionArn)));

                var accountId = GetAccountId(functionArn);
                if (accountId != null)
                {
                    tags.Add(new(AWSLambdaSemanticConventions.AttributeCloudAccountID, accountId));
                }
            }

            return tags;
        }

        private static string GetAccountId(string functionArn)
        {
            // The fifth item of function arn: https://github.com/open-telemetry/opentelemetry-specification/blob/86aeab1e0a7e6c67be09c7f15ff25063ee6d2b5c/specification/trace/semantic_conventions/instrumentation/aws-lambda.md#all-triggers
            // Function arn format - arn:aws:lambda:<region>:<account-id>:function:<function-name>

            var items = functionArn.Split(':');
            if (items.Length >= 5)
            {
                return items[4];
            }

            return null;
        }

        private static string GetFaasId(string functionArn)
        {
            var faasId = functionArn;

            // According to faas.id description https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/instrumentation/aws-lambda.md#all-triggers
            // the 8th part of arn (function version or alias, see https://docs.aws.amazon.com/lambda/latest/dg/lambda-api-permissions-ref.html)
            // should not be included into faas.id
            var items = functionArn.Split(':');
            if (items.Length >= 8)
            {
                faasId = string.Join(":", items.Take(7));
            }

            return faasId;
        }

        private static string GetFaasTrigger<TInput>(TInput input)
        {
            var trigger = "other";
            if (input is APIGatewayProxyRequest || input is APIGatewayHttpApiV2ProxyRequest)
            {
                trigger = "http";
            }

            return trigger;
        }

        private static ActivityContext ParseXRayTraceHeader(string rawHeader)
        {
            var xrayPropagator = new AWSXRayPropagator();

            var carrier = new Dictionary<string, string>()
            {
                { AWSXRayTraceHeaderKey, rawHeader },
            };

            var propagationContext = xrayPropagator.Extract(default, carrier, Getter);
            return propagationContext.ActivityContext;
        }

        private static IEnumerable<string> GetHeaderValues(APIGatewayProxyRequest request, string name)
        {
            if (request.MultiValueHeaders != null &&
                request.MultiValueHeaders.TryGetValue(name, out var values))
            {
                return values;
            }

            return null;
        }

        private static IEnumerable<string> GetHeaderValues(APIGatewayHttpApiV2ProxyRequest request, string name)
        {
            if (request.Headers != null &&
                request.Headers.TryGetValue(name, out var header))
            {
                // Multiple values for the same header will be separated by a comma.
                return header?.Split(',');
            }

            return null;
        }
    }
}
