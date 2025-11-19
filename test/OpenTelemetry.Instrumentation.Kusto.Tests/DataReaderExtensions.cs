// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;

namespace OpenTelemetry.Instrumentation.Kusto.Tests;

internal static class DataReaderExtensions
{
    public static void Consume(this IDataReader reader)
    {
        while (reader.Read())
        {
        }
    }
}
