// <copyright file="OtlpEtwExporterOptions.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Etw;
public class OtlpEtwExporterOptions
{
    private int _metricExporterIntervalMilliseconds = 60000;
    private int _maxEtwEventSizeBytes = 32 * 1024; // Can be max ~64KB

    /// <summary>
    /// Gets or sets the metric export interval in milliseconds. The default value is 60000.
    /// </summary>
    public int MetricExportIntervalMilliseconds
    {
        get
        {
            return this._metricExporterIntervalMilliseconds;
        }

        set
        {
            Guard.ThrowIfOutOfRange(value, min: 1000);

            this._metricExporterIntervalMilliseconds = value;
        }
    }

    /// <summary>
    /// Gets or sets the max ETW size in bytes. The default value is 32 KiB.
    /// </summary>
    public int MaxEtwEventSizeBytes
    {
        get
        {
            return this._maxEtwEventSizeBytes;
        }

        set
        {
            Guard.ThrowIfOutOfRange(value, min: 10/*1000*/, max: 63 * 1024);

            this._maxEtwEventSizeBytes = value;
        }
    }
}
