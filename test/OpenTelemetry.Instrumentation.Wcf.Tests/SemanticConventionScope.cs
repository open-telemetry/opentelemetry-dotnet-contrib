// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.Wcf.Tests;

internal static class SemanticConventionScope
{
    public static IDisposable Get(bool emitOldAttributes, bool emitNewAttributes)
    {
        var convention = (emitOldAttributes, emitNewAttributes) switch
        {
            (true, false) => RpcSemanticConventionHelper.RpcSemanticConvention.Old,
            (false, true) => RpcSemanticConventionHelper.RpcSemanticConvention.New,
            (true, true) => RpcSemanticConventionHelper.RpcSemanticConvention.Dupe,
            _ => throw new InvalidOperationException("At least one convention must be enabled."),
        };

        return Get(convention);
    }

    public static IDisposable Get(RpcSemanticConventionHelper.RpcSemanticConvention convention)
    {
        var value = convention switch
        {
            RpcSemanticConventionHelper.RpcSemanticConvention.Dupe => "rpc/dup",
            RpcSemanticConventionHelper.RpcSemanticConvention.New => "rpc",
            RpcSemanticConventionHelper.RpcSemanticConvention.Old or _ => string.Empty,
        };

        return EnvironmentVariableScope.Create("OTEL_SEMCONV_STABILITY_OPT_IN", value);
    }
}
