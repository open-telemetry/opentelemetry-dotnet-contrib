// See https://aka.ms/new-console-template for more information
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using System.Diagnostics;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;

Console.WriteLine("Hello, World!");

var serviceName = "MyServiceName";
var serviceVersion = "1.0.0";

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(serviceName)
    .SetResourceBuilder(ResourceBuilder
        .CreateDefault()
        .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
    .AddConsoleExporter()
    .AddOtlpExporter()
    .SetSampler(AWSXRayRemoteSampler.Builder()
                                    .SetPollingInterval(TimeSpan.FromSeconds(10))
                                    .SetEndpoint("http://localhost:2000")
                                    .Build())
    .Build();

using var source = new ActivitySource(serviceName);
//using var activity = source.StartActivity("MyActivity");
//activity?.SetTag("MyTag", "MyValue");
//activity?.Stop();

Console.WriteLine("Main thread id: " + Thread.CurrentThread.ManagedThreadId);

// making sure main thread is not blocked when polling rules
while (true)
{
    Console.WriteLine("Main thread sleeping...");
    Thread.Sleep(10000);
    Console.WriteLine("Main thread woken up");
}

// Console.ReadKey();
