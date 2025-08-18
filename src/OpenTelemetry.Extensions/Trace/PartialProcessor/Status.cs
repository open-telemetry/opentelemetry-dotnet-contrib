// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json.Serialization;

namespace OpenTelemetry.Extensions.Trace.PartialProcessor;

/// <summary>
/// Status per spec https://github.com/open-telemetry/opentelemetry-proto/blob/main/opentelemetry/proto/trace/v1/trace.proto.
/// </summary>
public class Status
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Status"/> class with an unset status.
    /// </summary>
    /// <param name="activityStatus">ActivityStatusCode.</param>
    /// <param name="activityStatusDescription">Activity status description.</param>
    public Status(ActivityStatusCode activityStatus, string? activityStatusDescription)
    {
        this.Code = activityStatus switch
        {
            ActivityStatusCode.Unset => StatusCode.Unset,
            ActivityStatusCode.Ok => StatusCode.Ok,
            ActivityStatusCode.Error => StatusCode.Error,
            _ => StatusCode.Unset,
        };
        this.Message = activityStatusDescription;
    }

    /// <summary>
    /// Status codes as per spec.
    /// </summary>
    public enum StatusCode
    {
        /// <summary>
        /// Status code is unset.
        /// </summary>
        Unset = 0,

        /// <summary>
        /// Status code is OK.
        /// </summary>
        Ok = 1,

        /// <summary>
        /// Status code is an error.
        /// </summary>
        Error = 2,
    }

    /// <summary>
    /// Gets or sets the status message.
    /// </summary>
    [JsonPropertyName("message")]
    [JsonInclude]
    internal string? Message { get; set; }

    /// <summary>
    /// Gets or sets the status code.
    /// </summary>
    [JsonPropertyName("code")]
    [JsonInclude]
    internal StatusCode Code { get; set; }
}
