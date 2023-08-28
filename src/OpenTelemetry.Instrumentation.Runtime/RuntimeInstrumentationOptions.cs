// <copyright file="RuntimeInstrumentationOptions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.Runtime;

/// <summary>
/// Options to define the runtime metrics.
/// </summary>
public class RuntimeInstrumentationOptions
{
    /*
            /// <summary>
            /// Gets or sets a value indicating whether garbage collection metrics should be collected.
            /// </summary>
            public bool? GcEnabled { get; set; }

    #if NET6_0_OR_GREATER
            /// <summary>
            /// Gets or sets a value indicating whether jitter metrics should be collected.
            /// </summary>
            public bool? JitEnabled { get; set; }
    #endif

    #if NET6_0_OR_GREATER
            /// <summary>
            /// Gets or sets a value indicating whether threading metrics should be collected.
            /// </summary>
            public bool? ThreadingEnabled { get; set; }
    #endif

            /// <summary>
            /// Gets or sets a value indicating whether assembly metrics should be collected.
            /// </summary>
            public bool? AssembliesEnabled { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether exception count metrics should be collected.
            /// </summary>
            public bool? ExceptionCountEnabled { get; set; }

            /// <summary>
            /// Gets a value indicating whether all metrics are enabled.
            /// </summary>
            internal bool IsAllEnabled => this.GcEnabled == null
    #if NET6_0_OR_GREATER
            && this.JitEnabled == null
    #endif
    #if NET6_0_OR_GREATER
            && this.ThreadingEnabled == null
    #endif
            && this.AssembliesEnabled == null
            && this.ExceptionCountEnabled == null;

            /// <summary>
            /// Gets a value indicating whether garbage collection metrics is enabled.
            /// </summary>
            internal bool IsGcEnabled => this.GcEnabled == true || this.IsAllEnabled;

    #if NET6_0_OR_GREATER
            /// <summary>
            /// Gets a value indicating whether jitter metrics is enabled.
            /// </summary>
            internal bool IsJitEnabled => this.JitEnabled == true || this.IsAllEnabled;
    #endif

    #if NET6_0_OR_GREATER
            /// <summary>
            /// Gets a value indicating whether threading metrics is enabled.
            /// </summary>
            internal bool IsThreadingEnabled => this.ThreadingEnabled == true || this.IsAllEnabled;
    #endif

            /// <summary>
            /// Gets a value indicating whether assembly metrics is enabled.
            /// </summary>
            internal bool IsAssembliesEnabled => this.AssembliesEnabled == true || this.IsAllEnabled;

            /// <summary>
            /// Gets a value indicating whether exception count metrics is enabled.
            /// </summary>
            internal bool IsExceptionCountEnabled => this.ExceptionCountEnabled == true || this.IsAllEnabled;
    */
}
