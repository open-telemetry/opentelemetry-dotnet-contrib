// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Extensions.Trace.StateActivityProcessor;

/// <summary>
/// SpanKind per spec.
/// </summary>
public enum SpanKind
{
    /// <summary>
    /// Unspecified per spec.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Internal per spec.
    /// </summary>
    Internal = 1,

    /// <summary>
    /// Server per spec.
    /// </summary>
    Server = 2,

    /// <summary>
    /// Client per spec.
    /// </summary>
    Client = 3,

    /// <summary>
    /// Producer per spec.
    /// </summary>
    Producer = 4,

    /// <summary>
    /// Consumer per spec.
    /// </summary>
    Consumer = 5,
}
