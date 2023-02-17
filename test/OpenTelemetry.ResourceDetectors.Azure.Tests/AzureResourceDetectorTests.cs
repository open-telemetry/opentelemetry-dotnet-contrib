// <copyright file="AzureResourceDetectorTests.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.ResourceDetectors.Azure.Tests;

public class AzureResourceDetectorTests : IDisposable
{
    [Fact]
    public void AppServiceResourceDetectorReturnsResourceWithAttributes()
    {
        Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", "AzureAppService");
        Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", "AzureInstance");
        var resource = ResourceBuilder.CreateEmpty().AddDetector(new AppServiceResourceDetector()).Build();
        Assert.NotNull(resource);
        Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "AzureAppService"), resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceInstance, "AzureInstance"), resource.Attributes);
    }

    [Fact]
    public void AppServiceResourceDetectorReturnsNullOutsideOfAppService()
    {
        var resource = new AppServiceResourceDetector().Detect();
        Assert.Empty(resource.Attributes);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", null);
        Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", null);
    }
}
