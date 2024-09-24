// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from 'src\OpenTelemetry.SemanticConventions\scripts\templates\registry\SemanticConventionsAttributes.cs.j2' </auto-generated>

#nullable enable

#pragma warning disable CS1570 // XML comment has badly formed XML

namespace OpenTelemetry.SemanticConventions;

/// <summary>
/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
/// </summary>
public static class RpcAttributes
{
    /// <summary>
    /// The <a href="https://connect.build/docs/protocol/#error-codes">error codes</a> of the Connect request. Error codes are always string values.
    /// </summary>
    public const string AttributeRpcConnectRpcErrorCode = "rpc.connect_rpc.error_code";

    /// <summary>
    /// Connect request metadata, <c><key></c> being the normalized Connect Metadata key (lowercase), the value being the metadata values.
    /// </summary>
    /// <remarks>
    /// Instrumentations SHOULD require an explicit configuration of which metadata values are to be captured. Including all request metadata values can be a security risk - explicit configuration helps avoid leaking sensitive information.
    /// </remarks>
    public const string AttributeRpcConnectRpcRequestMetadataTemplate = "rpc.connect_rpc.request.metadata";

    /// <summary>
    /// Connect response metadata, <c><key></c> being the normalized Connect Metadata key (lowercase), the value being the metadata values.
    /// </summary>
    /// <remarks>
    /// Instrumentations SHOULD require an explicit configuration of which metadata values are to be captured. Including all response metadata values can be a security risk - explicit configuration helps avoid leaking sensitive information.
    /// </remarks>
    public const string AttributeRpcConnectRpcResponseMetadataTemplate = "rpc.connect_rpc.response.metadata";

    /// <summary>
    /// gRPC request metadata, <c><key></c> being the normalized gRPC Metadata key (lowercase), the value being the metadata values.
    /// </summary>
    /// <remarks>
    /// Instrumentations SHOULD require an explicit configuration of which metadata values are to be captured. Including all request metadata values can be a security risk - explicit configuration helps avoid leaking sensitive information.
    /// </remarks>
    public const string AttributeRpcGrpcRequestMetadataTemplate = "rpc.grpc.request.metadata";

    /// <summary>
    /// gRPC response metadata, <c><key></c> being the normalized gRPC Metadata key (lowercase), the value being the metadata values.
    /// </summary>
    /// <remarks>
    /// Instrumentations SHOULD require an explicit configuration of which metadata values are to be captured. Including all response metadata values can be a security risk - explicit configuration helps avoid leaking sensitive information.
    /// </remarks>
    public const string AttributeRpcGrpcResponseMetadataTemplate = "rpc.grpc.response.metadata";

    /// <summary>
    /// The <a href="https://github.com/grpc/grpc/blob/v1.33.2/doc/statuscodes.md">numeric status code</a> of the gRPC request.
    /// </summary>
    public const string AttributeRpcGrpcStatusCode = "rpc.grpc.status_code";

    /// <summary>
    /// <c>error.code</c> property of response if it is an error response.
    /// </summary>
    public const string AttributeRpcJsonrpcErrorCode = "rpc.jsonrpc.error_code";

    /// <summary>
    /// <c>error.message</c> property of response if it is an error response.
    /// </summary>
    public const string AttributeRpcJsonrpcErrorMessage = "rpc.jsonrpc.error_message";

    /// <summary>
    /// <c>id</c> property of request or response. Since protocol allows id to be int, string, <c>null</c> or missing (for notifications), value is expected to be cast to string for simplicity. Use empty string in case of <c>null</c> value. Omit entirely if this is a notification.
    /// </summary>
    public const string AttributeRpcJsonrpcRequestId = "rpc.jsonrpc.request_id";

    /// <summary>
    /// Protocol version as in <c>jsonrpc</c> property of request/response. Since JSON-RPC 1.0 doesn't specify this, the value can be omitted.
    /// </summary>
    public const string AttributeRpcJsonrpcVersion = "rpc.jsonrpc.version";

    /// <summary>
    /// Compressed size of the message in bytes.
    /// </summary>
    public const string AttributeRpcMessageCompressedSize = "rpc.message.compressed_size";

    /// <summary>
    /// MUST be calculated as two different counters starting from <c>1</c> one for sent messages and one for received message.
    /// </summary>
    /// <remarks>
    /// This way we guarantee that the values will be consistent between different implementations.
    /// </remarks>
    public const string AttributeRpcMessageId = "rpc.message.id";

    /// <summary>
    /// Whether this is a received or sent message.
    /// </summary>
    public const string AttributeRpcMessageType = "rpc.message.type";

    /// <summary>
    /// Uncompressed size of the message in bytes.
    /// </summary>
    public const string AttributeRpcMessageUncompressedSize = "rpc.message.uncompressed_size";

    /// <summary>
    /// The name of the (logical) method being called, must be equal to the $method part in the span name.
    /// </summary>
    /// <remarks>
    /// This is the logical name of the method from the RPC interface perspective, which can be different from the name of any implementing method/function. The <c>code.function</c> attribute may be used to store the latter (e.g., method actually executing the call on the server side, RPC client stub method on the client side).
    /// </remarks>
    public const string AttributeRpcMethod = "rpc.method";

    /// <summary>
    /// The full (logical) name of the service being called, including its package name, if applicable.
    /// </summary>
    /// <remarks>
    /// This is the logical name of the service from the RPC interface perspective, which can be different from the name of any implementing class. The <c>code.namespace</c> attribute may be used to store the latter (despite the attribute name, it may include a class name; e.g., class with method actually executing the call on the server side, RPC client stub class on the client side).
    /// </remarks>
    public const string AttributeRpcService = "rpc.service";

    /// <summary>
    /// A string identifying the remoting system. See below for a list of well-known identifiers.
    /// </summary>
    public const string AttributeRpcSystem = "rpc.system";

    /// <summary>
    /// The <a href="https://connect.build/docs/protocol/#error-codes">error codes</a> of the Connect request. Error codes are always string values.
    /// </summary>
    public static class RpcConnectRpcErrorCodeValues
    {
        /// <summary>
        /// cancelled.
        /// </summary>
        public const string Cancelled = "cancelled";

        /// <summary>
        /// unknown.
        /// </summary>
        public const string Unknown = "unknown";

        /// <summary>
        /// invalid_argument.
        /// </summary>
        public const string InvalidArgument = "invalid_argument";

        /// <summary>
        /// deadline_exceeded.
        /// </summary>
        public const string DeadlineExceeded = "deadline_exceeded";

        /// <summary>
        /// not_found.
        /// </summary>
        public const string NotFound = "not_found";

        /// <summary>
        /// already_exists.
        /// </summary>
        public const string AlreadyExists = "already_exists";

        /// <summary>
        /// permission_denied.
        /// </summary>
        public const string PermissionDenied = "permission_denied";

        /// <summary>
        /// resource_exhausted.
        /// </summary>
        public const string ResourceExhausted = "resource_exhausted";

        /// <summary>
        /// failed_precondition.
        /// </summary>
        public const string FailedPrecondition = "failed_precondition";

        /// <summary>
        /// aborted.
        /// </summary>
        public const string Aborted = "aborted";

        /// <summary>
        /// out_of_range.
        /// </summary>
        public const string OutOfRange = "out_of_range";

        /// <summary>
        /// unimplemented.
        /// </summary>
        public const string Unimplemented = "unimplemented";

        /// <summary>
        /// internal.
        /// </summary>
        public const string Internal = "internal";

        /// <summary>
        /// unavailable.
        /// </summary>
        public const string Unavailable = "unavailable";

        /// <summary>
        /// data_loss.
        /// </summary>
        public const string DataLoss = "data_loss";

        /// <summary>
        /// unauthenticated.
        /// </summary>
        public const string Unauthenticated = "unauthenticated";
    }

    /// <summary>
    /// The <a href="https://github.com/grpc/grpc/blob/v1.33.2/doc/statuscodes.md">numeric status code</a> of the gRPC request.
    /// </summary>
    public static class RpcGrpcStatusCodeValues
    {
        /// <summary>
        /// OK.
        /// </summary>
        public const int Ok = 0;

        /// <summary>
        /// CANCELLED.
        /// </summary>
        public const int Cancelled = 1;

        /// <summary>
        /// UNKNOWN.
        /// </summary>
        public const int Unknown = 2;

        /// <summary>
        /// INVALID_ARGUMENT.
        /// </summary>
        public const int InvalidArgument = 3;

        /// <summary>
        /// DEADLINE_EXCEEDED.
        /// </summary>
        public const int DeadlineExceeded = 4;

        /// <summary>
        /// NOT_FOUND.
        /// </summary>
        public const int NotFound = 5;

        /// <summary>
        /// ALREADY_EXISTS.
        /// </summary>
        public const int AlreadyExists = 6;

        /// <summary>
        /// PERMISSION_DENIED.
        /// </summary>
        public const int PermissionDenied = 7;

        /// <summary>
        /// RESOURCE_EXHAUSTED.
        /// </summary>
        public const int ResourceExhausted = 8;

        /// <summary>
        /// FAILED_PRECONDITION.
        /// </summary>
        public const int FailedPrecondition = 9;

        /// <summary>
        /// ABORTED.
        /// </summary>
        public const int Aborted = 10;

        /// <summary>
        /// OUT_OF_RANGE.
        /// </summary>
        public const int OutOfRange = 11;

        /// <summary>
        /// UNIMPLEMENTED.
        /// </summary>
        public const int Unimplemented = 12;

        /// <summary>
        /// INTERNAL.
        /// </summary>
        public const int Internal = 13;

        /// <summary>
        /// UNAVAILABLE.
        /// </summary>
        public const int Unavailable = 14;

        /// <summary>
        /// DATA_LOSS.
        /// </summary>
        public const int DataLoss = 15;

        /// <summary>
        /// UNAUTHENTICATED.
        /// </summary>
        public const int Unauthenticated = 16;
    }

    /// <summary>
    /// Whether this is a received or sent message.
    /// </summary>
    public static class RpcMessageTypeValues
    {
        /// <summary>
        /// sent.
        /// </summary>
        public const string Sent = "SENT";

        /// <summary>
        /// received.
        /// </summary>
        public const string Received = "RECEIVED";
    }

    /// <summary>
    /// A string identifying the remoting system. See below for a list of well-known identifiers.
    /// </summary>
    public static class RpcSystemValues
    {
        /// <summary>
        /// gRPC.
        /// </summary>
        public const string Grpc = "grpc";

        /// <summary>
        /// Java RMI.
        /// </summary>
        public const string JavaRmi = "java_rmi";

        /// <summary>
        /// .NET WCF.
        /// </summary>
        public const string DotnetWcf = "dotnet_wcf";

        /// <summary>
        /// Apache Dubbo.
        /// </summary>
        public const string ApacheDubbo = "apache_dubbo";

        /// <summary>
        /// Connect RPC.
        /// </summary>
        public const string ConnectRpc = "connect_rpc";
    }
}
