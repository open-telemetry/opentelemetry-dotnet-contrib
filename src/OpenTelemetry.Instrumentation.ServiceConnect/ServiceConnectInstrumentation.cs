using System;
using OpenTelemetry.Instrumentation.ServiceConnect.Implementation;

namespace OpenTelemetry.Instrumentation.ServiceConnect;

internal class ServiceConnectInstrumentation : IDisposable
{
    private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

    public ServiceConnectInstrumentation(ServiceConnectInstrumentationOptions? options)
    {
        this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(
            name => new ServiceConnectDiagnosticListener(name),
            listener => listener.Name == ServiceConnectDiagnosticListener.DiagnosticSourceName,
            null,
           null);
        this.diagnosticSourceSubscriber.Subscribe();
    }

    public void Dispose()
    {
        this.diagnosticSourceSubscriber?.Dispose();
    }
}
