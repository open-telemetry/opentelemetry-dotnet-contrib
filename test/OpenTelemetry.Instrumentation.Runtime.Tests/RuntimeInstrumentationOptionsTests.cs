// <copyright file="RuntimeInstrumentationOptionsTests.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.Runtime.Tests;

public class RuntimeInstrumentationOptionsTests
{
    /*
            [Fact]
            public void Enable_All_If_Nothing_Was_Defined()
            {
                var options = new RuntimeInstrumentationOptions();

                Assert.True(options.IsGcEnabled);
    #if NET6_0_OR_GREATER
                Assert.True(options.IsJitEnabled);
                Assert.True(options.IsThreadingEnabled);
    #endif
                Assert.True(options.IsAssembliesEnabled);
                Assert.True(options.IsAllEnabled);
            }

            [Fact]
            public void Enable_Gc_Only()
            {
                var options = new RuntimeInstrumentationOptions { GcEnabled = true };

                Assert.True(options.IsGcEnabled);
    #if NET6_0_OR_GREATER
                Assert.False(options.IsJitEnabled);
                Assert.False(options.IsThreadingEnabled);
    #endif
                Assert.False(options.IsAssembliesEnabled);
                Assert.False(options.IsAllEnabled);
            }

    #if NET6_0_OR_GREATER
            [Fact]
            public void Enable_Jit_Only()
            {
                var options = new RuntimeInstrumentationOptions { JitEnabled = true };

                Assert.False(options.IsGcEnabled);
                Assert.True(options.IsJitEnabled);
                Assert.False(options.IsThreadingEnabled);
                Assert.False(options.IsAssembliesEnabled);
                Assert.False(options.IsAllEnabled);
            }
    #endif

    #if NET6_0_OR_GREATER
            [Fact]
            public void Enable_Threading_Only()
            {
                var options = new RuntimeInstrumentationOptions { ThreadingEnabled = true };

                Assert.False(options.IsGcEnabled);
                Assert.False(options.IsJitEnabled);
                Assert.True(options.IsThreadingEnabled);
                Assert.False(options.IsAssembliesEnabled);
                Assert.False(options.IsAllEnabled);
            }
    #endif

            [Fact]
            public void Enable_Assemblies_Only()
            {
                var options = new RuntimeInstrumentationOptions { AssembliesEnabled = true };

                Assert.False(options.IsGcEnabled);
    #if NET6_0_OR_GREATER
                Assert.False(options.IsJitEnabled);
                Assert.False(options.IsThreadingEnabled);
    #endif
                Assert.True(options.IsAssembliesEnabled);
                Assert.False(options.IsAllEnabled);
            }

            [Fact]
            public void Enable_Multiple()
            {
                var options = new RuntimeInstrumentationOptions { GcEnabled = true, AssembliesEnabled = true };

                Assert.True(options.IsGcEnabled);
    #if NET6_0_OR_GREATER
                Assert.False(options.IsJitEnabled);
                Assert.False(options.IsThreadingEnabled);
    #endif
                Assert.True(options.IsAssembliesEnabled);
                Assert.False(options.IsAllEnabled);
            }
    */
}
