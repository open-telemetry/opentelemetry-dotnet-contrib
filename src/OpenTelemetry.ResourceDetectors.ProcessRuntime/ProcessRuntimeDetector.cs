// <copyright file="ProcessRuntimeDetector.cs" company="OpenTelemetry Authors">
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
using System.Runtime.InteropServices;
using OpenTelemetry.Resources;

namespace OpenTelemetry.ResourceDetectors.ProcessRuntime;

/// <summary>
/// Process runtime detector.
/// </summary>
public class ProcessRuntimeDetector : IResourceDetector
{
    /// <summary>
    /// Detects the resource attributes from .NET runtime.
    /// </summary>
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        return new Resource(new List<KeyValuePair<string, object>>(3)
        {
            new(ProcessRuntimeSemanticConventions.AttributeProcessRuntimeDescription, RuntimeInformation.FrameworkDescription),
#if NETFRAMEWORK
            new(ProcessRuntimeSemanticConventions.AttributeProcessRuntimeName, ".NET Framework"),
#else
            new(ProcessRuntimeSemanticConventions.AttributeProcessRuntimeName, ".NET"),
#endif
            new(ProcessRuntimeSemanticConventions.AttributeProcessRuntimeVersion, Environment.Version.ToString()),
        });
    }
}
