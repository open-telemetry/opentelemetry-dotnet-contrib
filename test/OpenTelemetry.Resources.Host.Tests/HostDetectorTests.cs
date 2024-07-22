// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Resources.Host.Tests;

public class HostDetectorTests
{
    private const string MacOSMachineIdOutput = @"+-o J293AP  <class IOPlatformExpertDevice, id 0x100000227, registered, matched,$
        {
          ""IOPolledInterface"" = ""AppleARMWatchdogTimerHibernateHandler is not seria$
          ""#address-cells"" = <02000000>
          ""AAPL,phandle"" = <01000000>
          ""serial-number"" = <432123465233514651303544000000000000000000000000000000$
          ""IOBusyInterest"" = ""IOCommand is not serializable""
          ""target-type"" = <""J293"">
          ""platform-name"" = <743831303300000000000000000000000000000000000000000000$
          ""secure-root-prefix"" = <""md"">
          ""name"" = <""device-tree"">
          ""region-info"" = <4c4c2f41000000000000000000000000000000000000000000000000$
          ""manufacturer"" = <""Apple Inc."">
          ""compatible"" = <""J293AP"",""MacBookPro17,1"",""AppleARM"">
          ""config-number"" = <000000000000000000000000000000000000000000000000000000$
          ""IOPlatformSerialNumber"" = ""A01BC3QFQ05D""
          ""regulatory-model-number"" = <41323333380000000000000000000000000000000000$
          ""time-stamp"" = <""Mon Jun 27 20:12:10 PDT 2022"">
          ""clock-frequency"" = <00366e01>
          ""model"" = <""MacBookPro17,1"">
          ""mlb-serial-number"" = <432123413230363030455151384c4c314a0000000000000000$
          ""model-number"" = <4d59443832000000000000000000000000000000000000000000000$
          ""IONWInterrupts"" = ""IONWInterrupts""
          ""model-config"" = <""SUNWAY;MoPED=0x803914B08BE6C5AF0E6C990D7D8240DA4CAC2FF$
          ""device_type"" = <""bootrom"">
          ""#size-cells"" = <02000000>
          ""IOPlatformUUID"" = ""1AB2345C-03E4-57D4-A375-1234D48DE123""
        }";

    private static readonly IEnumerable<string> ETCMACHINEID = new[] { "Samples/etc_machineid" };
    private static readonly IEnumerable<string> ETCVARDBUSMACHINEID = new[] { "Samples/etc_var_dbus_machineid" };

    [Fact]
    public void TestHostAttributes()
    {
        var resource = ResourceBuilder.CreateEmpty().AddHostDetector().Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);

        Assert.Equal(2, resourceAttributes.Count);

        Assert.NotEmpty(resourceAttributes[HostSemanticConventions.AttributeHostName]);
        Assert.NotEmpty(resourceAttributes[HostSemanticConventions.AttributeHostId]);
    }

    [Fact]
    public void TestHostMachineIdLinux()
    {
        var combos = new[]
        {
            (Enumerable.Empty<string>(), null),
            (ETCMACHINEID, "etc_machineid"),
            (ETCVARDBUSMACHINEID, "etc_var_dbus_machineid"),
            (Enumerable.Concat(ETCMACHINEID, ETCVARDBUSMACHINEID), "etc_machineid"),
        };

        foreach (var (path, expected) in combos)
        {
            var detector = new HostDetector(
                PlatformID.Unix,
                () => path,
                () => throw new Exception("should not be called"),
                () => throw new Exception("should not be called"));
            var resource = ResourceBuilder.CreateEmpty().AddDetector(detector).Build();
            var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);

            if (string.IsNullOrEmpty(expected))
            {
                Assert.False(resourceAttributes.ContainsKey(HostSemanticConventions.AttributeHostId));
            }
            else
            {
                Assert.NotEmpty(resourceAttributes[HostSemanticConventions.AttributeHostId]);
                Assert.Equal(expected, resourceAttributes[HostSemanticConventions.AttributeHostId]);
            }
        }
    }

    [Fact]
    public void TestHostMachineIdMacOs()
    {
        var detector = new HostDetector(
            PlatformID.MacOSX,
            () => Enumerable.Empty<string>(),
            () => MacOSMachineIdOutput,
            () => throw new Exception("should not be called"));
        var resource = ResourceBuilder.CreateEmpty().AddDetector(detector).Build();
        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);
        Assert.NotEmpty(resourceAttributes[HostSemanticConventions.AttributeHostId]);
        Assert.Equal("1AB2345C-03E4-57D4-A375-1234D48DE123", resourceAttributes[HostSemanticConventions.AttributeHostId]);
    }

    [Fact]
    public void TestParseMacOsOutput()
    {
        var id = HostDetector.ParseMacOsOutput(MacOSMachineIdOutput);
        Assert.Equal("1AB2345C-03E4-57D4-A375-1234D48DE123", id);
    }

    [Fact]
    public void TestHostMachineIdWindows()
    {
        var detector = new HostDetector(
            PlatformID.Win32NT,
            () => Enumerable.Empty<string>(),
            () => throw new Exception("should not be called"),
            () => "windows-machine-id");
        var resource = ResourceBuilder.CreateEmpty().AddDetector(detector).Build();
        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);
        Assert.NotEmpty(resourceAttributes[HostSemanticConventions.AttributeHostId]);
        Assert.Equal("windows-machine-id", resourceAttributes[HostSemanticConventions.AttributeHostId]);
    }
}
