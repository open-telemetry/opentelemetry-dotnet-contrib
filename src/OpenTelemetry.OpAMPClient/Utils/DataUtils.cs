// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Opamp.Protocol;
using OpenTelemetry.OpAMPClient.Data;

namespace OpenTelemetry.OpAMPClient.Utils;

internal static class DataUtils
{
    public static AnyValue ToAnyValue(this AnyValueUnion value) => value.Type switch
    {
        AnyValueType.String => new AnyValue() { StringValue = value.StringValue },
        AnyValueType.Int => new AnyValue() { IntValue = value.IntValue!.Value },
        AnyValueType.Double => new AnyValue() { DoubleValue = value.DoubleValue!.Value },
        AnyValueType.Bool => new AnyValue() { BoolValue = value.BoolValue!.Value },
        _ => throw new NotSupportedException($"Unsupported AnyValue type: {value.Type}"),
    };
}
