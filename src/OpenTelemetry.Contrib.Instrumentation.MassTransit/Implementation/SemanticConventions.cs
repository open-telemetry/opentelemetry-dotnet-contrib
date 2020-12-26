// <copyright file="SemanticConventions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Contrib.Instrumentation.MassTransit.Implementation
{
    internal static class SemanticConventions
    {
        public const string AttributeNetTransport = "net.transport";
        public const string AttributeNetPeerIp = "net.peer.ip";
        public const string AttributeNetPeerPort = "net.peer.port";
        public const string AttributeNetPeerName = "net.peer.name";
        public const string AttributeNetHostIp = "net.host.ip";
        public const string AttributeNetHostPort = "net.host.port";
        public const string AttributeNetHostName = "net.host.name";

        public const string AttributeMessagingSystem = "messaging.system";
        public const string AttributeMessagingDestination = "messaging.destination";
        public const string AttributeMessagingDestinationKind = "messaging.destination_kind";
        public const string AttributeMessagingTempDestination = "messaging.temp_destination";
        public const string AttributeMessagingProtocol = "messaging.protocol";
        public const string AttributeMessagingProtocolVersion = "messaging.protocol_version";
        public const string AttributeMessagingUrl = "messaging.url";
        public const string AttributeMessagingMessageId = "messaging.message_id";
        public const string AttributeMessagingConversationId = "messaging.conversation_id";
        public const string AttributeMessagingPayloadSize = "messaging.message_payload_size_bytes";
        public const string AttributeMessagingPayloadCompressedSize = "messaging.message_payload_compressed_size_bytes";
        public const string AttributeMessagingOperation = "messaging.operation";

        public const string AttributeMessagingMassTransitInitiatorId = "messaging.masstransit.initiator_id";
        public const string AttributeMessagingMassTransitCorrelationId = "messaging.masstransit.correlation_id";
        public const string AttributeMessagingMassTransitConsumerType = "messaging.masstransit.consumer_type";
    }
}
