// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;

namespace OpenTelemetry.Instrumentation.Kusto.Tests;

internal static class DataReaderExtensions
{
    /// <summary>
    /// Consumes all results from an <see cref="IDataReader"/>.
    /// </summary>
    /// <param name="reader">The <see cref="IDataReader"/> to consume.</param>
    public static void Consume(this IDataReader reader)
    {
        do
        {
            while (reader.Read())
            {
                // Intentionally no-op
            }
        }
        while (reader.NextResult());
    }
}
