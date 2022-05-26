// <copyright file="ErrorActivityProcessorTests.cs" company="OpenTelemetry Authors">
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
    public class ErrorActivityProcessorTests
    {
        private ErrorActivityProcessor errorActivityProcessor = new ErrorActivityProcessor();

        [Fact]
        public async Task Process_ErrorStatusCodeIsSet()
        {
            Activity activity = new Activity("testOperationName");
            activity.SetStatus(ActivityStatusCode.Error, "TestErrorDesc");
            InstanaSpan instanaSpan = new InstanaSpan();
            await this.errorActivityProcessor.ProcessAsync(activity, instanaSpan);

            Assert.True(instanaSpan.Ec == 1);
            Assert.NotNull(instanaSpan.Data);
            Assert.NotNull(instanaSpan.Data.data);
            Assert.Equal("Error", instanaSpan.Data.data[InstanaExporterConstants.ERROR_FIELD]);
            Assert.Equal("TestErrorDesc", instanaSpan.Data.data[InstanaExporterConstants.ERROR_DETAIL_FIELD]);
        }

        [Fact]
        public async Task Process_ExistsExceptionEvent()
        {
            Activity activity = new Activity("testOperationName");
            InstanaSpan instanaSpan = new InstanaSpan() { TransformInfo = new OpenTelemetry.Exporter.Instana.Implementation.InstanaSpanTransformInfo() { HasExceptionEvent = true } };
            await this.errorActivityProcessor.ProcessAsync(activity, instanaSpan);

            Assert.True(instanaSpan.Ec == 1);
        }

        [Fact]
        public async Task Process_NoError()
        {
            Activity activity = new Activity("testOperationName");
            InstanaSpan instanaSpan = new InstanaSpan() { TransformInfo = new OpenTelemetry.Exporter.Instana.Implementation.InstanaSpanTransformInfo() };
            await this.errorActivityProcessor.ProcessAsync(activity, instanaSpan);

            Assert.True(instanaSpan.Ec == 0);
        }
    }
}
