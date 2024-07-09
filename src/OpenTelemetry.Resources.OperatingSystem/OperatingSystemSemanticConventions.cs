// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources.OperatingSystem;

internal static class OperatingSystemSemanticConventions
{
    public const string AttributeOperatingSystemType = "os.type";

    public static readonly string[] OperatingSystems =
        ["windows", "linux", "darwin"];
}
