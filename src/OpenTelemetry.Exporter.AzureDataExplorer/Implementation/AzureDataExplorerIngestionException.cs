// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

/// <summary>
/// Raised when an Azure Data Explorer REST request fails.
/// </summary>
#pragma warning disable CA1032 // Implement standard exception constructors - internal exception with required state.
#pragma warning disable CA1064 // Exceptions should be public - never surfaced to callers; exports report failures via the EventSource.
internal sealed class AzureDataExplorerIngestionException : Exception
#pragma warning restore CA1064
#pragma warning restore CA1032
{
    public AzureDataExplorerIngestionException(
        string message,
        bool isPermanent,
        HttpStatusCode? statusCode = null,
        string? errorCode = null,
        Exception? innerException = null,
        string? failureSubCode = null)
        : base(message, innerException)
    {
        this.IsPermanent = isPermanent;
        this.StatusCode = statusCode;
        this.ErrorCode = errorCode;
        this.FailureSubCode = failureSubCode;
    }

    /// <summary>
    /// Gets a value indicating whether retrying the request cannot succeed.
    /// </summary>
    public bool IsPermanent { get; }

    /// <summary>
    /// Gets the HTTP status code of the failed request, if any.
    /// </summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// Gets the Kusto error code (or exception type) of the failed request, if any.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Gets the Kusto error <c>code</c> (for example
    /// <c>BadRequest_StreamingIngestionPolicyNotEnabled</c>) of the failed request, if any.
    /// </summary>
    public string? FailureSubCode { get; }

    /// <summary>
    /// Determines whether retrying the operation that raised <paramref name="exception"/> cannot succeed.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns><see langword="true"/> when the error is permanent.</returns>
    public static bool IsPermanentError(Exception exception) => exception switch
    {
        AzureDataExplorerIngestionException ingestion => ingestion.IsPermanent,
        AggregateException aggregate when aggregate.InnerExceptions.Count == 1 => IsPermanentError(aggregate.InnerExceptions[0]),
        ArgumentException => true,
        _ => false,
    };
}
