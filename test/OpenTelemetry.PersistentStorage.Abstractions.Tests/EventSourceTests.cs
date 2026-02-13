// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.PersistentStorage.Abstractions.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_PersistentStorageAbstractionsEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<PersistentStorageAbstractionsEventSource>();
}
