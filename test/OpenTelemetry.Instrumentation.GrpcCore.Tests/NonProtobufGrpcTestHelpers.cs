// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Grpc.Core;

namespace OpenTelemetry.Instrumentation.GrpcCore.Tests;

internal static class NonProtobufGrpcTestHelpers
{
    internal static readonly Marshaller<NonProtobufPayload> PayloadMarshaller =
        new(static _ => [], static _ => new NonProtobufPayload());

    internal static readonly Method<NonProtobufPayload, NonProtobufPayload> UnaryMethod =
        new(
            MethodType.Unary,
            "OpenTelemetry.Instrumentation.GrpcCore.Tests.Foobar",
            "Unary",
            PayloadMarshaller,
            PayloadMarshaller);
}
