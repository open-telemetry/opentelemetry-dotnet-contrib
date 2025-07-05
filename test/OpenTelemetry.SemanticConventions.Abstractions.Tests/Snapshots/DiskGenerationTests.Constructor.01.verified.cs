//HintName: OtelAttributes.Disk.AttributeValues.g.cs

namespace OpenTelemetry.SemanticConventions;

#pragma warning disable CS8981
#pragma warning disable IDE1006
#pragma warning disable SA1629
internal partial struct Disk
{
    internal partial struct disk
    {
    internal partial struct io
    {
    internal partial struct direction
    {
        /// <summary>
        /// The disk IO operation direction.
        /// </summary>
        /// <remarks>This is the key for an attribute/tag.</remarks>
        internal const string read = "read";
    }
    }
    }
    internal partial struct disk
    {
    internal partial struct io
    {
    internal partial struct direction
    {
        /// <summary>
        /// The disk IO operation direction.
        /// </summary>
        /// <remarks>This is the key for an attribute/tag.</remarks>
        internal const string write = "write";
    }
    }
    }
}
#pragma warning restore SA1629
#pragma warning restore IDE1006
#pragma warning restore CS8981
