// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

/// <summary>
/// Holds the ActivitySource used to emit Kusto spans.
/// </summary>
internal static class KustoActivitySource
{
    public static readonly Version SemanticConventionsVersion = new(1, 33, 0);

    public static readonly ActivitySource ActivitySource = ActivitySourceFactory.Create(typeof(KustoActivitySource), SemanticConventionsVersion);

    public static readonly string ActivitySourceName = ActivitySource.Name;
}
