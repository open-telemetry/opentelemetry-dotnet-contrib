// <copyright file="RuntimeMetricsOptionsTests.cs" company="OpenTelemetry Authors">
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

using Xunit;

namespace OpenTelemetry.Instrumentation.Runtime.Tests
{
    public class RuntimeMetricsOptionsTests
    {
        [Fact]
        public void Enable_All_If_Nothing_Was_Defined()
        {
            var options = new RuntimeMetricsOptions();

            Assert.True(options.GetGcOption == Options.GcMetricOptions.All);
#if NET6_0_OR_GREATER
            Assert.True(options.GetJitOption == Options.JitMetricOptions.All);
#endif
#if NETCOREAPP3_1_OR_GREATER
            Assert.True(options.GetThreadingOption == Options.ThreadingMetricOptions.All);
#endif
            Assert.True(options.GetAssemblyOption == Options.AssemblyMetricOptions.All);
            Assert.True(options.GetExceptionOption == Options.ExceptionMetricOptions.All);
            Assert.True(options.IsDefault);
        }

        [Fact]
        public void Enable_Gc_Only()
        {
            var options = new RuntimeMetricsOptions { GcMetricOption = Options.GcMetricOptions.All };

            Assert.True(options.GetGcOption == Options.GcMetricOptions.All);
#if NET6_0_OR_GREATER
            Assert.True(options.GetJitOption == Options.JitMetricOptions.None);
#endif
#if NETCOREAPP3_1_OR_GREATER
            Assert.True(options.GetThreadingOption == Options.ThreadingMetricOptions.None);
#endif
            Assert.True(options.GetAssemblyOption == Options.AssemblyMetricOptions.None);
            Assert.True(options.GetExceptionOption == Options.ExceptionMetricOptions.None);
            Assert.False(options.IsDefault);
        }

#if NET6_0_OR_GREATER
        [Fact]
        public void Enable_Jit_Only()
        {
            var options = new RuntimeMetricsOptions { JitMetricOption = Options.JitMetricOptions.All };

            Assert.True(options.GetGcOption == Options.GcMetricOptions.None);
            Assert.True(options.GetJitOption == Options.JitMetricOptions.All);
            Assert.True(options.GetThreadingOption == Options.ThreadingMetricOptions.None);
            Assert.True(options.GetAssemblyOption == Options.AssemblyMetricOptions.None);
            Assert.True(options.GetExceptionOption == Options.ExceptionMetricOptions.None);
            Assert.False(options.IsDefault);
        }
#endif

#if NETCOREAPP3_1_OR_GREATER
        [Fact]
        public void Enable_Threading_Only()
        {
            var options = new RuntimeMetricsOptions { ThreadingMetricOption = Options.ThreadingMetricOptions.All };

            Assert.True(options.GetGcOption == Options.GcMetricOptions.None);
#if NET6_0_OR_GREATER
            Assert.True(options.GetJitOption == Options.JitMetricOptions.None);
#endif
            Assert.True(options.GetThreadingOption == Options.ThreadingMetricOptions.All);
            Assert.True(options.GetAssemblyOption == Options.AssemblyMetricOptions.None);
            Assert.True(options.GetExceptionOption == Options.ExceptionMetricOptions.None);
            Assert.False(options.IsDefault);
        }
#endif

        [Fact]
        public void Enable_Assemblies_Only()
        {
            var options = new RuntimeMetricsOptions { AssemblyMetricOption = Options.AssemblyMetricOptions.All };

            Assert.True(options.GetGcOption == Options.GcMetricOptions.None);
#if NET6_0_OR_GREATER
            Assert.True(options.GetJitOption == Options.JitMetricOptions.None);
#endif
#if NETCOREAPP3_1_OR_GREATER
            Assert.True(options.GetThreadingOption == Options.ThreadingMetricOptions.None);
#endif
            Assert.True(options.GetAssemblyOption == Options.AssemblyMetricOptions.All);
            Assert.True(options.GetExceptionOption == Options.ExceptionMetricOptions.None);
            Assert.False(options.IsDefault);
        }

        [Fact]
        public void Enable_Exceptions_Only()
        {
            var options = new RuntimeMetricsOptions { ExceptionMetricOption = Options.ExceptionMetricOptions.All };

            Assert.True(options.GetGcOption == Options.GcMetricOptions.None);
#if NET6_0_OR_GREATER
            Assert.True(options.GetJitOption == Options.JitMetricOptions.None);
#endif
#if NETCOREAPP3_1_OR_GREATER
            Assert.True(options.GetThreadingOption == Options.ThreadingMetricOptions.None);
#endif
            Assert.True(options.GetAssemblyOption == Options.AssemblyMetricOptions.None);
            Assert.True(options.GetExceptionOption == Options.ExceptionMetricOptions.All);
            Assert.False(options.IsDefault);
        }

        [Fact]
        public void Enable_Multiple()
        {
            var options = new RuntimeMetricsOptions { GcMetricOption = Options.GcMetricOptions.All, AssemblyMetricOption = Options.AssemblyMetricOptions.All };

            Assert.True(options.GetGcOption == Options.GcMetricOptions.All);
#if NET6_0_OR_GREATER
            Assert.True(options.GetJitOption == Options.JitMetricOptions.None);
#endif
#if NETCOREAPP3_1_OR_GREATER
            Assert.True(options.GetThreadingOption == Options.ThreadingMetricOptions.None);
#endif
            Assert.True(options.GetAssemblyOption == Options.AssemblyMetricOptions.All);
            Assert.True(options.GetExceptionOption == Options.ExceptionMetricOptions.None);
            Assert.False(options.IsDefault);
        }
    }
}
