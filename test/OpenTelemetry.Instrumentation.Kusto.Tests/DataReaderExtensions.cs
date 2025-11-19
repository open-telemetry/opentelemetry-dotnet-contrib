// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using System.Diagnostics;
using System.Text;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using OpenTelemetry.Instrumentation.Kusto.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

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