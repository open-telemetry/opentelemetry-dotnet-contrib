// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using System.Diagnostics.Tracing;

namespace OpenTelemetry.Exporter.Geneva.Transports;

internal sealed class EtwDataTransport : IDataTransport, IDisposable
{
    private readonly EtwEventSource eventSource;
    private bool disposed;

    public EtwDataTransport(string providerName)
    {
        this.eventSource = new EtwEventSource(providerName);
    }

    public void Send(byte[] data, int size)
    {
        this.eventSource.SendEvent(data, size);
    }

    public bool IsEnabled()
    {
        return this.eventSource.IsEnabled();
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.eventSource.Dispose();

        this.disposed = true;
    }

    internal sealed class EtwEventSource : EventSource
    {
        public EtwEventSource(string providerName)
            : base(providerName, EventSourceSettings.EtwManifestEventFormat)
        {
        }

        public enum EtwEventId
        {
            TraceEvent = 100,
        }

#pragma warning disable CA1822 // Mark members as static

        /// <summary>
        /// Dummy _data_ field is present so that when <see cref="SendEvent"/> is called, the event has at least one
        /// parameter with <see cref="EtwEventId.TraceEvent"/> and single data object, it matches the runtime metadata
        /// in ETW manifest.
        /// </summary>
        /// <param name="data">
        /// Dummy placeholder for <see cref="EventSource"/> reflection-based ETW manifest generation.
        /// Even though de-facto it's <c>byte[]</c> payload, <c>byte[]</c> when unwrapped to ETW manifest generation
        /// is augmented with <c>size</c> metadata, and becomes two fields.
        /// Geneva treats this only single parameter as messagepack raw blob and doesn't use manifest at all.
        /// </param>
        [Event((int)EtwEventId.TraceEvent, Version = 1, Level = EventLevel.Informational)]
        public void InformationalEvent(byte[] data)
        {
        }

#pragma warning restore CA1822 // Mark members as static

        [NonEvent]
#if NET
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode", Justification = "WriteEventCore is safe when eventData object is a primitive type, which it is in this case.")]
#endif
        public unsafe void SendEvent(byte[] data, int size)
        {
            EventData* dataDesc = stackalloc EventData[2];
            fixed (byte* ptr = data)
            {
                dataDesc[0].DataPointer = (IntPtr)(&size);
                dataDesc[0].Size = 4;
                dataDesc[1].DataPointer = (IntPtr)ptr;
                dataDesc[1].Size = size;
                this.WriteEventCore((int)EtwEventId.TraceEvent, 2, dataDesc);
            }
        }
    }
}
