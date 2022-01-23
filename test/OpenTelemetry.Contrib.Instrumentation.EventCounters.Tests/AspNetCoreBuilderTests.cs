﻿// <copyright file="AspNetCoreBuilderTests.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Contrib.Instrumentation.EventCounters.AspNetCore;
using Xunit;

namespace OpenTelemetry.Contrib.Instrumentation.EventCounters.Tests
{
    public class AspNetCoreBuilderTests
    {
        [Fact]
        public void WithAll_Adds_All_Known_Counter()
        {
            // retrieve all interface methods
            var methodCounts = typeof(IAspNetCoreBuilder).GetMethods().Length;

            var options = new EventCountersOptions();
            var builder = new AspNetCoreBuilder(options);
            builder.WithAll();

            Assert.NotEmpty(options.Sources);
            Assert.Equal(options.Sources[0].EventCounters.Count, methodCounts - 2);
        }
    }
}
