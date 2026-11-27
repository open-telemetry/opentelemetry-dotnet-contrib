// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using System.Diagnostics.Tracing;

namespace OpenTelemetry.Exporter.Geneva.Transports;

/// <summary>
/// Base class for <see cref="EventSource"/>s that write a single raw blob as the event payload.
/// </summary>
/// <remarks>
/// <see cref="EventSource.WriteEventCore"/> takes a pointer to an <c>EventData</c>, so calling it cannot be
/// done without unsafe code. This class keeps all of that in one place, in a single <c>unsafe</c> block.
/// </remarks>
internal abstract class RawEventSource : EventSource
{
    protected RawEventSource()
    {
    }

    protected RawEventSource(string eventSourceName, EventSourceSettings settings)
        : base(eventSourceName, settings)
    {
    }

    /// <summary>
    /// Writes <paramref name="size"/> bytes of <paramref name="data"/>, starting at <paramref name="offset"/>,
    /// as the (only) payload of the event.
    /// </summary>
    /// <param name="eventId">The event id.</param>
    /// <param name="data">The buffer containing the payload.</param>
    /// <param name="offset">The offset of the payload within <paramref name="data"/>.</param>
    /// <param name="size">The size of the payload.</param>
    [NonEvent]
#if NET
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode", Justification = "WriteEventCore is safe when eventData object is a primitive type, which it is in this case.")]
#endif
    protected void WriteRawEvent(int eventId, byte[] data, int offset, int size)
    {
        unsafe
        {
            var dataDesc = stackalloc EventData[1];
            fixed (byte* ptr = data)
            {
                dataDesc[0].DataPointer = (nint)ptr + offset;
                dataDesc[0].Size = size;
                this.WriteEventCore(eventId, 1, dataDesc);
            }
        }
    }
}
