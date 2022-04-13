// <copyright file="TagName.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.MassTransit.Implementation
{
    internal class TagName
    {
        public const string SpanKind = "span.kind";

        public const string PeerAddress = "peer.address";
        public const string PeerHost = "peer.host";
        public const string PeerService = "peer.service";
        public const string SourceHostMachine = "source-host-machine";

        public const string MessageId = "message-id";
        public const string ConversationId = "conversation-id";
        public const string CorrelationId = "correlation-id";
        public const string InitiatorId = "initiator-id";

        public const string ConsumerType = "consumer-type";
        public const string MessageTypes = "message-types";

        public const string DestinationAddress = "destination-address";
        public const string SourceAddress = "source-address";
        public const string InputAddress = "input-address";
    }
}
