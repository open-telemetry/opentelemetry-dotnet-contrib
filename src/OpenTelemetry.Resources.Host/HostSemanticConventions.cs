// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources.Host;

internal static class HostSemanticConventions
{
    public const string AttributeHostName = "host.name";
    public const string AttributeHostId = "host.id";
    public const string AttributeHostArch = "host.arch";
    public const string AttributeHostIp = "host.ip";
    public const string AttributeHostMac = "host.mac";
    public const string AttributeHostCpuVendorId = "host.cpu.vendor.id";
    public const string AttributeHostCpuFamily = "host.cpu.family";
    public const string AttributeHostCpuModelId = "host.cpu.model.id";
    public const string AttributeHostCpuModelName = "host.cpu.model.name";
    public const string AttributeHostCpuStepping = "host.cpu.stepping";
    public const string AttributeHostCpuCacheL2Size = "host.cpu.cache.l2.size";
}
