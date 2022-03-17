// <copyright file="WcfInstrumentationActivitySource.cs" company="OpenTelemetry Authors">
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
using System.ServiceModel.Channels;

namespace OpenTelemetry.Instrumentation.Wcf
{
    /// <summary>
    /// WCF instrumentation.
    /// </summary>
    internal static class WcfInstrumentationActivitySource
    {
        public const string ActivitySourceName = "OpenTelemetry.WCF";
        public const string IncomingRequestActivityName = ActivitySourceName + ".IncomingRequest";
        public const string OutgoingRequestActivityName = ActivitySourceName + ".OutgoingRequest";

        private static readonly Version Version = typeof(WcfInstrumentationActivitySource).Assembly.GetName().Version;

        public static ActivitySource ActivitySource { get; } = new ActivitySource(ActivitySourceName, Version.ToString());

        public static Func<Message, string, IEnumerable<string>> MessageHeaderValuesGetter { get; }
            = (request, name) =>
            {
                var headerIndex = request.Headers.FindHeader(name, "https://www.w3.org/TR/trace-context/");
                return headerIndex < 0
                    ? null
                    : new[] { request.Headers.GetHeader<string>(headerIndex) };
            };

        public static Action<Message, string, string> MessageHeaderValueSetter { get; }
            = (request, name, value) => request.Headers.Add(MessageHeader.CreateHeader(name, "https://www.w3.org/TR/trace-context/", value, false));

        public static WcfInstrumentationOptions Options { get; set; }
    }
}
