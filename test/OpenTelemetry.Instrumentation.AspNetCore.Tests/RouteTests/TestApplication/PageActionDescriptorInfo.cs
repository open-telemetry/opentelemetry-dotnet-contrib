// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable
using System.Text.Json.Serialization;

namespace RouteTests.TestApplication;

public class PageActionDescriptorInfo
{
    public PageActionDescriptorInfo()
    {
    }

    public PageActionDescriptorInfo(string relativePath, string viewEnginePath)
    {
        this.PageActionDescriptorRelativePath = relativePath;
        this.PageActionDescriptorViewEnginePath = viewEnginePath;
    }

    [JsonPropertyName("RelativePath")]
    public string PageActionDescriptorRelativePath { get; set; } = string.Empty;

    [JsonPropertyName("ViewEnginePath")]
    public string PageActionDescriptorViewEnginePath { get; set; } = string.Empty;
}
