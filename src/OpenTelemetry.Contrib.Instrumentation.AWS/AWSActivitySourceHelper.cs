// <copyright file="AWSActivitySourceHelper.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Reflection;

namespace OpenTelemetry.Contrib.Instrumentation.AWS;

/// <summary>
/// Helper class to hold common properties.
/// </summary>
internal static class AWSActivitySourceHelper
{
    internal static readonly AssemblyName AssemblyName = typeof(AWSActivitySourceHelper).Assembly.GetName();
    internal static readonly string ActivitySourceName = "Amazon.AWS.AWSClientInstrumentation";
    internal static readonly Version Version = AssemblyName.Version;
    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version.ToString());
}
