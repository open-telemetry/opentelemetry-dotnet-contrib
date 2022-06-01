// <copyright file="PersistentStorageEventSource.cs" company="OpenTelemetry Authors">
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
    internal sealed class PersistentStorageEventSource : EventSource
    {
        public static PersistentStorageEventSource Log = new PersistentStorageEventSource();
        private const string EventSourceName = "OpenTelemetry-Extensions-PersistentStorage";

        [NonEvent]
        public void PersistentStorageException(string className, string message, string path, Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.PersistentStorageException(className, message, path, ToInvariantString(ex));
            }
        }

        [Event(1, Message = "Exception occurred in {0}. Error Message: {1}. FilePath: '{2}'. Exception: {3}", Level = EventLevel.Error)]
        public void PersistentStorageException(string className, string message, string path, string ex)
        {
            this.WriteEvent(1, className, message, path, ex);
        }

        [Event(8, Message = "{0}: {1}", Level = EventLevel.Warning)]
        public void PersistentStorageWarning(string className, string message)
        {
            this.WriteEvent(2, className, message);
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
