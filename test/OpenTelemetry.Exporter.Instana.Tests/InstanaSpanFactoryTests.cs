// <copyright file="InstanaSpanFactoryTests.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Exporter.Instana.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.Instana.Tests;

public static class InstanaSpanFactoryTests
{
    [Fact]
    public static void CreateSpan()
    {
        _ = new InstanaSpanFactory();
        InstanaSpan instanaSpan = InstanaSpanFactory.CreateSpan();

        Assert.NotNull(instanaSpan);
        Assert.NotNull(instanaSpan.TransformInfo);
        Assert.NotNull(instanaSpan.Data);
        Assert.Empty(instanaSpan.Data.data);
        Assert.Empty(instanaSpan.Data.Tags);
        Assert.Empty(instanaSpan.Data.Events);
    }
}
