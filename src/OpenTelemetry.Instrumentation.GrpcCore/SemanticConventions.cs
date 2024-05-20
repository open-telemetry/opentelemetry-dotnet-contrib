// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.GrpcCore;

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

    // Used for unit testing only.
    internal const string AttributeActivityIdentifier = "activityidentifier";

    // https://github.com/open-telemetry/semantic-conventions/blob/main/docs/exceptions/exceptions-spans.md
    internal const string AttributeExceptionEventName = "exception";
    internal const string AttributeExceptionType = "exception.type";
    internal const string AttributeExceptionMessage = "exception.message";
    internal const string AttributeExceptionStacktrace = "exception.stacktrace";
#pragma warning restore SA1600 // Elements should be documented
}
