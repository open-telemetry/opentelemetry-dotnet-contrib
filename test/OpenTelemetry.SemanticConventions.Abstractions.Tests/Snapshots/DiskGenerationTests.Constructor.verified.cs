//HintName: OtelAttributes.Disk.g.cs

namespace OpenTelemetry.SemanticConventions;

internal partial struct Disk
{
    #pragma warning disable CS8981
    #pragma warning disable IDE1006
    #pragma warning disable SA1629
    internal partial struct disk
    {
    internal partial struct io
    {
        /// <summary>
        /// The disk IO operation direction.
        /// </summary>
        /// <remarks>This is the key for an attribute/tag.</remarks>
        internal const string direction = "disk.io.direction";
    }
    }
    #pragma warning restore SA1629
    #pragma warning restore IDE1006
    #pragma warning restore CS8981
}
