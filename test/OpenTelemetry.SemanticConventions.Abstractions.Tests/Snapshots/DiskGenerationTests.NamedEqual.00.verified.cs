//HintName: OtelAttributes.DiskAttributeNames.AttributeNames.g.cs

namespace OpenTelemetry.SemanticConventions.Example;

#pragma warning disable CS8981
#pragma warning disable IDE1006
#pragma warning disable SA1629
internal partial struct DiskAttributeNames
{
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
}
#pragma warning restore SA1629
#pragma warning restore IDE1006
#pragma warning restore CS8981
