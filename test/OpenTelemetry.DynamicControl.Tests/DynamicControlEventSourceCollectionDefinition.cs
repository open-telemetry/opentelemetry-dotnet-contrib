// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.DynamicControl.Tests;

// Event listeners capture the shared static source, so these tests must not overlap
// any other collection, including tests that emit events without listening.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DynamicControlEventSourceCollectionDefinition
{
    public const string Name = "DynamicControl EventSource collection";
}
