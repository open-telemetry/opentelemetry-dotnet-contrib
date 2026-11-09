// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Kusto.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class KustoActivitySourceCollectionDefinition
{
    public const string Name = "Kusto ActivitySource";
}
