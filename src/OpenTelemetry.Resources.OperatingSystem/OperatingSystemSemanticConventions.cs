// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources.OperatingSystem;

internal static class OperatingSystemSemanticConventions
{
    public const string AttributeOperatingSystemType = "os.type";
    public const string AttributeOperatingSystemBuildId = "os.build_id";
    public const string AttributeOperatingSystemDescription = "os.description";
    public const string AttributeOperatingSystemName = "os.name";
    public const string AttributeOperatingSystemVersion = "os.version";
    public const string AttributeOperatingSystemFamily = "os.family";

    public static string[] OSFamilyApple = ["darwin"];
    public static string[] OSFamilyWindows = ["windows"];
    public static string[] OSFamilyAndroid = ["aosp"];
}
