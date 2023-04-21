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
using Xunit;

namespace OpenTelemetry.ResourceDetectors.Azure.Tests;

public class AzureResourceDetectorTests : IDisposable
{
    [Fact]
    public void AppServiceResourceDetectorReturnsResourceWithAttributes()
    {
        try
        {
            foreach (var kvp in AppServiceResourceDetector.AppServiceResourceAttributes)
            {
                if (kvp.Value == "WEBSITE_SITE_NAME")
                {
                    continue;
                }

                Environment.SetEnvironmentVariable(kvp.Value, kvp.Key);
            }

            // Special case for service.name and appSrv_SiteName attribute
            Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", "ServiceName");
        }
        catch
        {
        }

        var resource = ResourceBuilder.CreateEmpty().AddDetector(new AppServiceResourceDetector()).Build();
        Assert.NotNull(resource);

        foreach (var kvp in AppServiceResourceDetector.AppServiceResourceAttributes)
        {
            if (kvp.Value == "WEBSITE_SITE_NAME")
            {
                Assert.Contains(new KeyValuePair<string, object>(kvp.Key, "ServiceName"), resource.Attributes);
            }
            else
            {
                Assert.Contains(new KeyValuePair<string, object>(kvp.Key, kvp.Key), resource.Attributes);
            }
        }
    }

    public void Dispose()
    {
        foreach (var kvp in AppServiceResourceDetector.AppServiceResourceAttributes)
        {
            Environment.SetEnvironmentVariable(kvp.Value, null);
        }
    }
}
