// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.OneCollector;

/// <summary>
/// Defines a callback delegate for handling payload transmitted events in the
/// <see cref="OneCollectorExporter{T}"/> class.
/// </summary>
/// <param name="args">Arguments.</param>
public delegate void OneCollectorExporterPayloadTransmittedCallbackAction(
    in OneCollectorExporterPayloadTransmittedCallbackArguments args);
