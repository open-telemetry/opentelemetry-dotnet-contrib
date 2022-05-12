// <copyright file="PersistentStorageAbstractionsEventSource.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Extensions.PersistentStorage
{
    [EventSource(Name = EventSourceName)]
    internal sealed class PersistentStorageAbstractionsEventSource : EventSource
    {
        public static PersistentStorageAbstractionsEventSource Log = new PersistentStorageAbstractionsEventSource();
        private const string EventSourceName = "OpenTelemetry-Extensions-PersistentStorage-Abstractions";

        [NonEvent]
        public void PersistentBlobException(string message, Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.PersistentBlobException(message, ToInvariantString(ex));
            }
        }

        [Event(1, Message = "{0} : {1}", Level = EventLevel.Error)]
        public void PersistentBlobException(string message, string ex)
        {
            this.WriteEvent(1, message, ex);
        }

        [NonEvent]
        public void PersistentBlobProviderException(string message, Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.PersistentBlobProviderException(message, ToInvariantString(ex));
            }
        }

        [Event(1, Message = "{0} : {1}", Level = EventLevel.Error)]
        public void PersistentBlobProviderException(string message, string ex)
        {
            this.WriteEvent(1, message, ex);
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
