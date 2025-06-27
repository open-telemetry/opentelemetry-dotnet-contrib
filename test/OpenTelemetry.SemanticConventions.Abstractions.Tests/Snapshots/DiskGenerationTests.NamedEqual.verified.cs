//HintName: OtelAttributes.DiskAttributeNames.g.cs

namespace OpenTelemetry.SemanticConventions.Example;

internal partial struct DiskAttributeNames 
{
    #pragma warning disable CS8981
    #pragma warning disable SA1629
    internal partial struct disk
    {
    internal partial struct io
    {
        /// <summary>
        /// The disk IO operation direction.
        /// </summary>
        /// <example>read</example>
        internal const string direction = "disk.io.direction";
    }
    }
    #pragma warning restore SA1629
    #pragma warning restore CS8981
}
