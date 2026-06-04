// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public sealed class WeaverCollection : ICollectionFixture<WeaverFixture>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    public const string Name = "Weaver collection";
}
