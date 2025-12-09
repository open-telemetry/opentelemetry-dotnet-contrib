// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

internal readonly record struct KustoStatementInfo(string? Summarized, string? Sanitized);
