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
using Amazon.Lambda.Core;
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

        internal static ActivityContext GetParentContext()
        {
            // Currently get trace header from Lambda runtime environment variable
            // https://docs.aws.amazon.com/lambda/latest/dg/configuration-envvars.html#configuration-envvars-runtime
            // TODO: Add steps to extract trace header from http header

            var traceHeaderKey = Environment.GetEnvironmentVariable(AWSXRayLambdaTraceHeaderKey);
            var traceHeader = ParseXRayTraceHeader(traceHeaderKey);
            return traceHeader;
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
            if (context != null)
            {
                return context.FunctionName;
            }

            return Environment.GetEnvironmentVariable(FunctionName);
        }

        internal static string GetFunctionVersion()
        {
            return Environment.GetEnvironmentVariable(FunctionVersion);
        }

        internal static string GetAccountId(string functionArn)
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
    }
}
