// <copyright file="TagsActivityProcessorTests.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using System.Threading.Tasks;
using OpenTelemetry.Exporter.Instana.Implementation;
using OpenTelemetry.Exporter.Instana.Implementation.Processors;
using Xunit;

namespace OpenTelemetry.Exporter.Instana.Tests.Processors
{
    public class TagsActivityProcessorTests
    {
        private TagsActivityProcessor tagsActivityProcessor = new TagsActivityProcessor();

        [Fact]
        public async Task ProcessAsync_StatusTagsExist()
        {
            Activity activity = new Activity("testOperationName");
            activity.AddTag("otel.status_code", "testStatusCode");
            activity.AddTag("otel.status_description", "testStatusDescription");
            activity.AddTag("otel.testTag", "testTag");

            InstanaSpan instanaSpan = new InstanaSpan();
            await this.tagsActivityProcessor.ProcessAsync(activity, instanaSpan);

            Assert.NotNull(instanaSpan.Data);
            Assert.NotNull(instanaSpan.Data.Tags);
            Assert.Contains(instanaSpan.Data.Tags, x => x.Key == "otel.testTag" && x.Value == "testTag");
            Assert.Equal("testStatusCode", instanaSpan.TransformInfo.StatusCode);
            Assert.Equal("testStatusDescription", instanaSpan.TransformInfo.StatusDesc);
        }

        [Fact]
        public async Task ProcessAsync_StatusTagsDoNotExist()
        {
            Activity activity = new Activity("testOperationName");
            activity.AddTag("otel.testTag", "testTag");

            InstanaSpan instanaSpan = new InstanaSpan();
            await this.tagsActivityProcessor.ProcessAsync(activity, instanaSpan);

            Assert.NotNull(instanaSpan.Data);
            Assert.NotNull(instanaSpan.Data.Tags);
            Assert.Contains(instanaSpan.Data.Tags, x => x.Key == "otel.testTag" && x.Value == "testTag");
            Assert.Equal(string.Empty, instanaSpan.TransformInfo.StatusCode);
            Assert.Equal(string.Empty, instanaSpan.TransformInfo.StatusDesc);
        }
    }
}
