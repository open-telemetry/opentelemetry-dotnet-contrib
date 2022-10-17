using OpenTelemetry;
using OpenTelemetry.Metrics;

public class Program
{
    public static void Main()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddProcessInstrumentation()
            .AddPrometheusHttpListener()
            .Build();

        Console.WriteLine(".NET Process metrics are available at http://localhost:9464/metrics, press any key to exit...");
        Console.ReadKey(false);
    }
}
