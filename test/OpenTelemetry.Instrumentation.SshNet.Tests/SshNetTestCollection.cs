// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.SshNet.Tests;

// Spans and metrics flow through static sources, so every test class that
// collects them must run serially.
[CollectionDefinition(Name, DisableParallelization = true)]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public sealed class SshNetTestCollection
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    public const string Name = "SshNet";
}
