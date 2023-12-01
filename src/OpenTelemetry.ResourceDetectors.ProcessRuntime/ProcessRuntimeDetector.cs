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

#if !NETFRAMEWORK
using System;
#endif
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
        var frameworkDescription = RuntimeInformation.FrameworkDescription;
        string netRuntimeName;
#if NETFRAMEWORK
        string netFrameworkVersion;
#endif

        var lastSpace = frameworkDescription.LastIndexOf(' ');
        if (lastSpace != -1)
        {
            // sample result '.NET Framework 4.8.9195.0'
            netRuntimeName = frameworkDescription.Substring(0, lastSpace);
#if NETFRAMEWORK
            netFrameworkVersion = frameworkDescription.Substring(lastSpace + 1);
#endif
        }
        else
        {
            // do not expect to be here, all checked implementation has common FrameworkDescription format - '{Name With Optional Spaces} {Version}'
            netRuntimeName = "unknown";
#if NETFRAMEWORK
            netFrameworkVersion = "unknown";
#endif
        }

        return new Resource(new List<KeyValuePair<string, object>>(3)
        {
            new(ProcessRuntimeSemanticConventions.AttributeProcessRuntimeDescription, frameworkDescription),
            new(ProcessRuntimeSemanticConventions.AttributeProcessRuntimeName, netRuntimeName),
#if NETFRAMEWORK
            new(ProcessRuntimeSemanticConventions.AttributeProcessRuntimeVersion, netFrameworkVersion),
#else
            new(ProcessRuntimeSemanticConventions.AttributeProcessRuntimeVersion, Environment.Version.ToString()),
#endif
        });
    }
}
