// <copyright file="AWSLambdaResourceDetector.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;

namespace OpenTelemetry.Contrib.Instrumentation.AWSLambda.Implementation
{
    internal static class AWSLambdaResourceDetector
    {
        /// <summary>
        /// Detect the resource attributes for AWS Lambda.
        /// </summary>
        /// <returns>List of resource attributes pairs.</returns>
        internal static IEnumerable<KeyValuePair<string, object>> Detect()
        {
            var resourceAttributes = new List<KeyValuePair<string, object>>()
            {
                new KeyValuePair<string, object>(AWSLambdaSemanticConventions.AttributeCloudProvider, AWSLambdaUtils.GetCloudProvider()),
                new KeyValuePair<string, object>(AWSLambdaSemanticConventions.AttributeCloudRegion, AWSLambdaUtils.GetAWSRegion()),
                new KeyValuePair<string, object>(AWSLambdaSemanticConventions.AttributeFaasName, AWSLambdaUtils.GetFunctionName()),
                new KeyValuePair<string, object>(AWSLambdaSemanticConventions.AttributeFaasVersion, AWSLambdaUtils.GetFunctionVersion()),
            };

            return resourceAttributes;
        }
    }
}
