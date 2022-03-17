// <copyright file="WcfEnrichEventNames.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.Wcf
{
    /// <summary>
    /// Constants used for event names when enriching an activity.
    /// </summary>
    public static class WcfEnrichEventNames
    {
#if NETFRAMEWORK
        /// <summary>
        /// WCF service activity, event happens before WCF service method is invoked.
        /// </summary>
        public const string AfterReceiveRequest = "AfterReceiveRequest";

        /// <summary>
        /// WCF service activity, event happens after the WCF service method is invoked but before the reply is sent back to the client.
        /// </summary>
        public const string BeforeSendReply = "BeforeSendReply";
#endif

        /// <summary>
        /// WCF client activity, event happens before the request is sent across the wire.
        /// </summary>
        public const string BeforeSendRequest = "BeforeSendRequest";

        /// <summary>
        /// WCF client activity, event happens after a reply from the WCF service is received.
        /// </summary>
        public const string AfterReceiveReply = "AfterReceiveReply";
    }
}
