// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Exporter.Geneva.Tests;

[Collection("OtlpProtobufMetricExporterTests")]
#pragma warning disable CA1515
public class OtlpProtobufMetricExporterWithoutPrefixTests : OtlpProtobufMetricExporterTests
#pragma warning restore CA1515
{
    protected override bool PrefixBufferWithUInt32LittleEndianLength => false;
}
