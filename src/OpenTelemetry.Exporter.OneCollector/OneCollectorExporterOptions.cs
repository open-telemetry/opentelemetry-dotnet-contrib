// <copyright file="OneCollectorExporterOptions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.OneCollector;

/// <summary>
/// Contains options for the <see cref="OneCollectorExporter{T}"/> class.
/// </summary>
public abstract class OneCollectorExporterOptions
{
    internal OneCollectorExporterOptions()
    {
    }

    /// <summary>
    /// Gets or sets the OneCollector tenant token.
    /// </summary>
    public string? TenantToken { get; set; }

    /// <summary>
    /// Gets or sets the OneCollector instrumentation key.
    /// </summary>
    public string? InstrumentationKey { get; set; }

    /// <summary>
    /// Gets the OneCollector transport options.
    /// </summary>
    public OneCollectorExporterTransportOptions TransportOptions { get; } = new();

    internal virtual void Validate()
    {
        if (string.IsNullOrWhiteSpace(this.TenantToken))
        {
            throw new InvalidOperationException($"{nameof(this.TenantToken)} was not specified on {this.GetType().Name} options.");
        }

        if (string.IsNullOrWhiteSpace(this.InstrumentationKey))
        {
            throw new InvalidOperationException($"{nameof(this.InstrumentationKey)} was not specified on {this.GetType().Name} options.");
        }

        this.TransportOptions.Validate();
    }
}
