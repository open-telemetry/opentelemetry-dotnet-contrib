// <copyright file="Utils.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Contrib.Instrumentation.AWS
{
    internal class Utils
    {
        internal static string RemoveSuffix(string originalString, string suffix)
        {
            if (originalString.EndsWith(suffix))
            {
                return originalString.Substring(0, originalString.Length - suffix.Length);
            }

            return originalString;
        }

        /// <summary>
        /// Removes amazon prefix from service name. There are two type of service name.
        ///     Amazon.DynamoDbV2
        ///     AmazonS3
        ///     .
        /// </summary>
        /// <param name="serviceName">Name of the service.</param>
        /// <returns>String after removing Amazon prefix.</returns>
        internal static string RemoveAmazonPrefixFromServiceName(string serviceName)
        {
            return RemovePrefix(RemovePrefix(serviceName, "Amazon"), ".");
        }

        private static string RemovePrefix(string originalString, string prefix)
        {
            if (originalString.StartsWith(prefix))
            {
                return originalString.Substring(prefix.Length);
            }

            return originalString;
        }
    }
}
