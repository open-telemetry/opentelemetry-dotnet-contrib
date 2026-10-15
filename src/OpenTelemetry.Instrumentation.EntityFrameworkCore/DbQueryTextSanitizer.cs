// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore;

/// <summary>
/// Sanitizes database query text before it is emitted on telemetry.
/// </summary>
/// <param name="context">The query text sanitization context.</param>
/// <returns>The query text sanitization result.</returns>
public delegate DbQueryTextSanitizationResult DbQueryTextSanitizer(DbQueryTextSanitizationContext context);
