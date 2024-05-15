using OpenTelemetry;
using OpenTelemetry.Exporter.Geneva;
using OpenTelemetry.Metrics;
using OpenTelemetry.PersistentStorage.FileSystem;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;

namespace MyApp;

internal class Program
{
    private static readonly Meter MyMeter = new("MyCompany.MyProduct.MyLibrary", "1.0");
    private static readonly Counter<long> MyFruitCounter = MyMeter.CreateCounter<long>("MyFruitCounter");
    private static readonly ActivitySource MyActivitySource = new("MyCompany.MyProduct.MyLibrary");
    private static readonly Histogram<long> MyHistogram = MyMeter.CreateHistogram<long>("MyHistogram");
    static void Main(string[] args)
    {
        using var persistentBlobProvider = new FileBlobProvider(@"C:\Users\vibankwa\source\repos\temp\data\traces");

        var data = Encoding.UTF8.GetBytes("Hello, World!");

        // Create blob.
        persistentBlobProvider.TryCreateBlob(data, out var createdBlob);

        // List all blobs.
        foreach (var blobItem in persistentBlobProvider.GetBlobs())
        {
            Console.WriteLine(((FileBlob)blobItem).FullPath);
        }

        // Get single blob.
        if (persistentBlobProvider.TryGetBlob(out var blob))
        {
            // Lease before reading
            if (blob.TryLease(1000))
            {
                // Read
                if (blob.TryRead(out var outputData))
                {
                    Console.WriteLine(Encoding.UTF8.GetString(outputData));
                }

                // Delete
                if (blob.TryDelete())
                {
                    Console.WriteLine("Successfully deleted the blob");
                }
            }
        }
        //        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
        //        .AddSource("MyCompany.MyProduct.MyLibrary")
        //        .Build();

        //        using var activity = MyActivitySource.StartActivity("SayHello");

        //        activity?.SetTag("foo", 1);
        //        activity?.SetTag("bar", "Hello, World!");
        //        activity?.SetTag("baz", new int[] { 1, 2, 3 });
        //        activity?.SetStatus(ActivityStatusCode.Ok);


        //#pragma warning disable OTEL1002 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        //        var meterProvider = Sdk.CreateMeterProviderBuilder()
        //        .SetExemplarFilter(ExemplarFilterType.AlwaysOn)
        //        .AddMeter("MyCompany.MyProduct.MyLibrary")
        //        .AddGenevaMetricExporter(o => o.ConnectionString = "Account={MetricAccount};Namespace={MetricNamespace};PrivatePreviewEnableOtlpProtobufEncoding=true")
        //        .Build();
        //#pragma warning restore OTEL1002 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        //        // In this example, we have low cardinality which is below the 2000
        //        // default limit. If you have high cardinality, you need to set the
        //        // cardinality limit properly.
        //        MyFruitCounter.Add(123, new("name", "apple"), new("color", "red"));
        //        //MyFruitCounter.Add(2, new("name", "lemon"), new("color", "yellow"));
        //        //MyFruitCounter.Add(1, new("name", "lemon"), new("color", "yellow"));
        //        //MyFruitCounter.Add(2, new("name", "apple"), new("color", "green"));
        //        //MyFruitCounter.Add(5, new("name", "apple"), new("color", "red"));
        //        //MyFruitCounter.Add(4, new("name", "lemon"), new("color", "yellow"));


        //        //MyHistogram.Record(123, new("name", "apple"), new("color", "red"));
        //        //MyHistogram.Record(2, new("name", "lemon"), new("color", "yellow"));
        //        //MyHistogram.Record(1, new("name", "lemon"), new("color", "yellow"));
        //        //MyHistogram.Record(2, new("name", "apple"), new("color", "green"));
        //        //MyHistogram.Record(5, new("name", "apple"), new("color", "red"));
        //        //MyHistogram.Record(4, new("name", "lemon"), new("color", "yellow"));

        //        // Dispose meter provider before the application ends.
        //        // This will flush the remaining metrics and shutdown the metrics pipeline.
        //        meterProvider.ForceFlush();

        Console.ReadLine();
    }
}
