// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics;

namespace OpenTelemetry.Extensions.Trace.StateActivityProcessor;

public class Status
{
    public Status(ActivityStatusCode activityStatus, string? activityStatusDescription)
    {
        this.Code = activityStatus switch
        {
            ActivityStatusCode.Unset => StatusCode.StatusCodeUnset,
            ActivityStatusCode.Ok => StatusCode.StatusCodeOk,
            ActivityStatusCode.Error => StatusCode.StatusCodeError,
            _ => StatusCode.StatusCodeUnset
        };
        this.Message = activityStatusDescription;
    }

    public enum StatusCode
    {
        StatusCodeUnset = 0,
        StatusCodeOk = 1,
        StatusCodeError = 2
    }

    public string? Message { get; set; }
    public StatusCode Code { get; set; } = StatusCode.StatusCodeUnset;
}
