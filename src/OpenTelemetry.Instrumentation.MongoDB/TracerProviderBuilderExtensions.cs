namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of Mongo instrumentation.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Adds Mongo instrumentation to the tracer provider.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddMongoDBInstrumentation(this TracerProviderBuilder builder)
        => builder.AddSource("MongoDB.Driver.Core.Extensions.DiagnosticSources");
}
