// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Settings;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class OpAmpClientSettingsTests
{
    [Fact]
    public void CustomMessageQueueLimits_HaveExpectedDefaults()
    {
        var settings = new OpAmpClientSettings();

        Assert.Equal(2048, settings.MaxPendingCustomMessages);
        Assert.Equal(64 * 1024 * 1024, settings.MaxPendingCustomMessageBytes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void MaxPendingCustomMessages_RejectsNonPositiveValue(int value)
    {
        var settings = new OpAmpClientSettings();

        Assert.Throws<ArgumentOutOfRangeException>(
            nameof(value),
            () => settings.MaxPendingCustomMessages = value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void MaxPendingCustomMessageBytes_RejectsNonPositiveValue(int value)
    {
        var settings = new OpAmpClientSettings();

        Assert.Throws<ArgumentOutOfRangeException>(
            nameof(value),
            () => settings.MaxPendingCustomMessageBytes = value);
    }
}
