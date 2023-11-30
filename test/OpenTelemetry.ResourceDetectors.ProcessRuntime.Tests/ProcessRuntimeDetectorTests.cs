// <copyright file="ProcessRuntimeDetectorTests.cs" company="OpenTelemetry Authors">
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

using System.Linq;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.ResourceDetectors.ProcessRuntime.Tests;

public class ProcessRuntimeDetectorTests
{
    [Fact]
    public void TestProcessRuntimeAttributes()
    {
        var resource = ResourceBuilder.CreateEmpty().AddDetector(new ProcessRuntimeDetector()).Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);

        Assert.Equal(3, resourceAttributes.Count);

#if NETFRAMEWORK
        Assert.Matches(@"^\.NET Framework \d+\.\d+\.\d+\.\d+$", resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeDescription]);
        Assert.Equal(".NET Framework", resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeName]);
        Assert.Matches(@"^4.[1-9]\d*\.\d+\.\d+$", resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeVersion]);
#else
        Assert.Matches(@"^\.NET \d+\.\d+\.\d+$", resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeDescription]);
        Assert.Equal(".NET", resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeName]);
        Assert.Matches(@"^\d+\.\d+\.\d+$", resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeVersion]);
#endif
    }
}
