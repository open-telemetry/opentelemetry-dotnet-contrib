using BenchmarkDotNet.Running;

namespace OpenTelemetry.Exporter.Geneva.Benchmark
{
    internal class Program
    {
        static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
