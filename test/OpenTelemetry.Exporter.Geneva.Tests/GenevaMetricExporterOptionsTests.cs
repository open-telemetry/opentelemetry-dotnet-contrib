// <copyright file="GenevaMetricExporterOptionsTests.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using Xunit;

namespace OpenTelemetry.Exporter.Geneva.Tests;

public class GenevaMetricExporterOptionsTests
{
    [Fact]
    public void InvalidPrepopulatedDimensions()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            var exporterOptions = new GenevaMetricExporterOptions { PrepopulatedMetricDimensions = null };
        });

        Assert.Throws<ArgumentNullException>(() =>
        {
            var exporterOptions = new GenevaMetricExporterOptions
            {
                PrepopulatedMetricDimensions = new Dictionary<string, object>
                {
                    ["DimensionKey"] = null,
                },
            };
        });

        var invalidDimensionNameException = Assert.Throws<ArgumentException>(() =>
        {
            var exporterOptions = new GenevaMetricExporterOptions
            {
                PrepopulatedMetricDimensions = new Dictionary<string, object>
                {
                    [new string('a', GenevaMetricExporter.MaxDimensionNameSize + 1)] = "DimensionValue",
                },
            };
        });

        var expectedErrorMessage = $"The dimension: {new string('a', GenevaMetricExporter.MaxDimensionNameSize + 1)} exceeds the maximum allowed limit of {GenevaMetricExporter.MaxDimensionNameSize} characters for a dimension name.";
        Assert.Equal(expectedErrorMessage, invalidDimensionNameException.Message);

        var invalidDimensionValueException = Assert.Throws<ArgumentException>(() =>
        {
            var exporterOptions = new GenevaMetricExporterOptions
            {
                PrepopulatedMetricDimensions = new Dictionary<string, object>
                {
                    ["DimensionKey"] = new string('a', GenevaMetricExporter.MaxDimensionValueSize + 1),
                },
            };
        });

        expectedErrorMessage = $"Value provided for the dimension: DimensionKey exceeds the maximum allowed limit of {GenevaMetricExporter.MaxDimensionValueSize} characters for dimension value.";
        Assert.Equal(expectedErrorMessage, invalidDimensionValueException.Message);
    }

    [Fact]
    public void MetricExportIntervalValidationTest()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var exporterOptions = new GenevaMetricExporterOptions
            {
                MetricExportIntervalMilliseconds = 999,
            };
        });

        var exception = Record.Exception(() =>
        {
            var exporterOptions = new GenevaMetricExporterOptions
            {
                MetricExportIntervalMilliseconds = 1000,
            };
        });

        Assert.Null(exception);
    }

    [Fact]
    public void InvalidMeterNamespaceOverrides()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            var exporterOptions = new GenevaMetricExporterOptions { MeterNamespaceOverrides = null };
        });

        Assert.Throws<ArgumentException>(() =>
        {
            var exporterOptions = new GenevaMetricExporterOptions
            {
                MeterNamespaceOverrides = new Dictionary<string, string>
                {
                    ["Meter"] = null,
                },
            };
        });

        Assert.Throws<ArgumentException>(() =>
        {
            var exporterOptions = new GenevaMetricExporterOptions
            {
                MeterNamespaceOverrides = new Dictionary<string, string>
                {
                    ["Meter"] = " ",
                },
            };
        });
    }
}
