// <copyright file="EtwDataTransport.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Diagnostics.Tracing;

namespace OpenTelemetry.Exporter.Geneva;

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

    [Event((int)EtwEventId.TraceEvent, Version = 1, Level = EventLevel.Informational)]
    public void InformationalEvent()
    {
    }

    [NonEvent]
    public unsafe void SendEvent(int eventId, byte[] data, int size)
    {
        EventData* dataDesc = stackalloc EventData[1];
        fixed (byte* ptr = data)
        {
            dataDesc[0].DataPointer = (IntPtr)ptr;
            dataDesc[0].Size = (int)size;
            this.WriteEventCore(eventId, 1, dataDesc);
        }
    }
}

internal sealed class EtwDataTransport : IDataTransport, IDisposable
{
    public EtwDataTransport(string providerName)
    {
        this.m_eventSource = new EtwEventSource(providerName);
    }

    public void Send(byte[] data, int size)
    {
        this.m_eventSource.SendEvent((int)EtwEventSource.EtwEventId.TraceEvent, data, size);
    }

    public bool IsEnabled()
    {
        return this.m_eventSource.IsEnabled();
    }

    private EtwEventSource m_eventSource;
    private bool m_disposed;

    public void Dispose()
    {
        if (this.m_disposed)
        {
            return;
        }

        this.m_eventSource.Dispose();

        this.m_disposed = true;
    }
}
