// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Geneva.Tests;

/// <summary>
/// Collection definition for ETW tests.
///
/// Since ETW is a system-global(not even process-global) resource, these tests shouldn't be run in parallel.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public sealed class EtwCollection
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    public const string Name = "Etw collection";
}
