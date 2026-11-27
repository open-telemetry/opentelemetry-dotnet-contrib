// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
        => this.eventSource.SendEvent((int)EtwEventSource.EtwEventId.TraceEvent, data, size);

    public bool IsEnabled()
        => this.eventSource.IsEnabled();

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.eventSource.Dispose();

        this.disposed = true;
    }

    internal sealed class EtwEventSource : RawEventSource
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

        // Must stay parameterless: the agent reads the raw ETW blob, and .NET prepends a 4-byte length to any
        // declared field, which the agent rejects with "Bad forward protocol format".
        [Event((int)EtwEventId.TraceEvent, Version = 1, Level = EventLevel.Informational)]
        public void InformationalEvent()
        {
        }

#pragma warning restore CA1822 // Mark members as static
        [NonEvent]
        public void SendEvent(int eventId, byte[] data, int size)
            => this.WriteRawEvent(eventId, data, 0, size);
    }
}
