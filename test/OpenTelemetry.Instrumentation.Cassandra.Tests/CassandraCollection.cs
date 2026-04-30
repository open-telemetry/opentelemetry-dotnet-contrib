// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Instrumentation.Cassandra.Tests;

[CollectionDefinition(Name)]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public sealed class CassandraCollection : ICollectionFixture<CassandraFixture>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    public const string Name = "Cassandra";
}
