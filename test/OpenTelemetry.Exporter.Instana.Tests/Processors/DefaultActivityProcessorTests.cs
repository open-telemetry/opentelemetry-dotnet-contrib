// <copyright file="DefaultActivityProcessorTests.cs" company="OpenTelemetry Authors">
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
    public class DefaultActivityProcessorTests
    {
        private DefaultActivityProcessor defaultActivityProcessor = new DefaultActivityProcessor();

        [Fact]
        public async Task ProcessAsync()
        {
            Activity activity = new Activity("testOperationName");
            activity.Start();
            await Task.Delay(200);
            activity.Stop();
            InstanaSpan instanaSpan = new InstanaSpan();
            await this.defaultActivityProcessor.ProcessAsync(activity, instanaSpan);

            Assert.False(string.IsNullOrEmpty(instanaSpan.S));
            Assert.False(string.IsNullOrEmpty(instanaSpan.Lt));
            Assert.True(instanaSpan.D > 0);
            Assert.True(instanaSpan.Ts > 0);
            Assert.NotNull(instanaSpan.Data);
            Assert.NotNull(instanaSpan.Data.data);
            Assert.Contains(instanaSpan.Data.data, filter: x =>
            {
                return x.Key == "kind"
                       && x.Value.Equals("internal");
            });
        }
    }
}
