// <copyright file="AWSXRayEventSource.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Threading;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay
{
    [EventSource(Name = "OpenTelemetry-AWS-XRay")]
    internal class AWSXRayEventSource : EventSource
    {
        public static AWSXRayEventSource Log = new AWSXRayEventSource();

        [NonEvent]
        public void ActivityContextExtractException(string format, Exception ex)
        {
            if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
            {
                this.FailedToExtractActivityContext(format, ToInvariantString(ex));
            }
        }

        [NonEvent]
        public void ResourceAttributesExtractException(string format, Exception ex)
        {
            if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
            {
                this.FailedToExtractResourceAttributes(format, ToInvariantString(ex));
            }
        }

        [Event(1, Message = "Failed to extract activity context in format: '{0}', context: '{1}'.", Level = EventLevel.Warning)]
        public void FailedToExtractActivityContext(string format, string exception)
        {
            this.WriteEvent(1, format, exception);
        }

        [Event(2, Message = "Failed to inject activity context in format: '{0}', context: '{1}'.", Level = EventLevel.Warning)]
        public void FailedToInjectActivityContext(string format, string error)
        {
            this.WriteEvent(2, format, error);
        }

        [Event(3, Message = "Failed to extract resource attributes in '{0}'.", Level = EventLevel.Warning)]
        public void FailedToExtractResourceAttributes(string format, string exception)
        {
            this.WriteEvent(3, format, exception);
        }

        /// <summary>
        /// Returns a culture-independent string representation of the given <paramref name="exception"/> object,
        /// appropriate for diagnostics tracing.
        /// </summary>
        private static string ToInvariantString(Exception exception)
        {
            var originalUICulture = Thread.CurrentThread.CurrentUICulture;

            try
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
                return exception.ToString();
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = originalUICulture;
            }
        }
    }
}
