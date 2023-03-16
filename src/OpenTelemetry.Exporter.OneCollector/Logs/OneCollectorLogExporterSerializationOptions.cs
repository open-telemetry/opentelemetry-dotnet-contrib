// <copyright file="OneCollectorLogExporterSerializationOptions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter.OneCollector;

/// <summary>
/// Contains serialization options used to build a <see
/// cref="OneCollectorExporter{T}"/> instance for exporting <see
/// cref="LogRecord"/> telemetry data.
/// </summary>
public sealed class OneCollectorLogExporterSerializationOptions
{
    /// <summary>
    /// Gets or sets the exception stack trace handling type. Default
    /// value: <see
    /// cref="OneCollectorExporterSerializationExceptionStackTraceHandlingType.Ignore"/>.
    /// </summary>
    public OneCollectorExporterSerializationExceptionStackTraceHandlingType ExceptionStackTraceHandling { get; set; }
        = OneCollectorExporterSerializationExceptionStackTraceHandlingType.Ignore;

    /// <summary>
    /// Gets or sets OneCollector serialization format. Default value: <see
    /// cref="OneCollectorExporterSerializationFormatType.CommonSchemaV4JsonStream"/>.
    /// </summary>
    internal OneCollectorExporterSerializationFormatType Format { get; set; } = OneCollectorExporterSerializationFormatType.CommonSchemaV4JsonStream;

#pragma warning disable CA1822 // Mark members as static
    internal void Validate()
#pragma warning restore CA1822 // Mark members as static
    {
    }
}
