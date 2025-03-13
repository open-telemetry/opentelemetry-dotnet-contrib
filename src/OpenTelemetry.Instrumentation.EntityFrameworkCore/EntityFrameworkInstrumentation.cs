// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.EntityFrameworkCore.Implementation;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore;

internal class EntityFrameworkInstrumentation : IDisposable
{
    private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;
    internal static int MetricHandles;
    internal static int TracingHandles;

    public EntityFrameworkInstrumentation(EntityFrameworkInstrumentationOptions? options)
    {
        this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(
            name => new EntityFrameworkDiagnosticListener(name, options),
            listener => listener.Name == EntityFrameworkDiagnosticListener.DiagnosticSourceName,
            null,
            EntityFrameworkInstrumentationEventSource.Log.UnknownErrorProcessingEvent);
        this.diagnosticSourceSubscriber.Subscribe();
    }

    public static EntityFrameworkInstrumentationOptions TracingOptions { get; set; } = new();

    public static IDisposable AddMetricHandle() => new MetricHandle();

    public static IDisposable AddTracingHandle() => new TracingHandle();

    public void Dispose()
    {
        this.diagnosticSourceSubscriber?.Dispose();
    }

    private sealed class MetricHandle : IDisposable
    {
        private bool disposed;

        public MetricHandle()
        {
            Interlocked.Increment(ref MetricHandles);
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                Interlocked.Decrement(ref MetricHandles);
                this.disposed = true;
            }
        }
    }

    private sealed class TracingHandle : IDisposable
    {
        private bool disposed;

        public TracingHandle()
        {
            Interlocked.Increment(ref TracingHandles);
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                Interlocked.Decrement(ref TracingHandles);
                this.disposed = true;
            }
        }
    }
}
