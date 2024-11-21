// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Exporter.Geneva.Tests;

[Collection("OtlpProtobufMetricExporterTests")]
internal class OtlpProtobufMetricExporterWithoutPrefixTests : OtlpProtobufMetricExporterTests
{
    protected override bool PrefixBufferWithUInt32LittleEndianLength => false;
}
