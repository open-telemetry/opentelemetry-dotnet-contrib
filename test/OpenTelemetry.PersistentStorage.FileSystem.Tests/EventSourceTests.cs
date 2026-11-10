// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;

namespace OpenTelemetry.PersistentStorage.FileSystem.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_PersistentStorageEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<PersistentStorageEventSource>();

    [Fact]
    public void EventSourceTests_PersistentStorageEventSource_WritesEvents() =>
        EventSourceTestHelper.ExerciseEventSource<PersistentStorageEventSource>();
}
