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

namespace OpenTelemetry.Instrumentation.GrpcCore
{
    /// <summary>
    /// Semantic conventions.
    /// </summary>
    internal static class SemanticConventions
    {
#pragma warning disable SA1600 // Elements should be documented
        public const string AttributeRpcSystem = "rpc.system";
        public const string AttributeRpcService = "rpc.service";
        public const string AttributeRpcMethod = "rpc.method";
        public const string AttributeRpcGrpcStatusCode = "rpc.grpc.status_code";
        public const string AttributeMessageType = "message.type";
        public const string AttributeMessageID = "message.id";
        public const string AttributeMessageCompressedSize = "message.compressed_size";
        public const string AttributeMessageUncompressedSize = "message.uncompressed_size";
        public const string AttributeOtelStatusCode = "otel.status_code";
        public const string AttributeOtelStatusDescription = "otel.status_description";

        // Used for unit testing only.
        internal const string AttributeActivityIdentifier = "activityidentifier";
#pragma warning restore SA1600 // Elements should be documented
    }
}
