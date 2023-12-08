// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using Cassandra.Mapping.Attributes;

namespace OpenTelemetry.Instrumentation.Cassandra.Tests;

[Table("books")]
public class BooksEntity
{
    public BooksEntity(Guid id, string name)
    {
        this.Id = id;
        this.Name = name;
    }

    [PartitionKey]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; }
}
