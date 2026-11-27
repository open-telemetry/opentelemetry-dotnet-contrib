// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.AzureDataExplorer.Tests;

/// <summary>
/// Runs integration tests on their own so the exporter EventSource listener
/// does not observe failures deliberately raised by parallel unit tests.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationTestsCollectionDefinition
{
    public const string Name = "AzureDataExplorerIntegration";
}
