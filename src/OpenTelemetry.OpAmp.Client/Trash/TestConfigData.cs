// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Trash;

internal static class TestConfigData
{
    public static string Yaml = """
        OTEL_VALUE_1: 'value1'
        OTEL_VALUE_2: 'value2'
        """;

    public static string Json = """
        {
            'OTEL_VALUE_1': 'value1',
            'OTEL_VALUE_2': 'value2'
        }
        """;
}
