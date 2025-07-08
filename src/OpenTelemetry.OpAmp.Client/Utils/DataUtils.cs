// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Protocol;
using OpenTelemetry.OpAmp.Client.Data;

namespace OpenTelemetry.OpAmp.Client.Utils;

internal static class DataUtils
{
    public static AnyValue ToAnyValue(this AnyValueUnion value) => value.Type switch
    {
        AnyValueType.String => new AnyValue() { StringValue = value.StringValue },
        AnyValueType.Integer => new AnyValue() { IntValue = value.IntValue!.Value },
        AnyValueType.Double => new AnyValue() { DoubleValue = value.DoubleValue!.Value },
        AnyValueType.Boolean => new AnyValue() { BoolValue = value.BoolValue!.Value },
        _ => throw new NotSupportedException($"Unsupported AnyValue type: {value.Type}"),
    };
}
