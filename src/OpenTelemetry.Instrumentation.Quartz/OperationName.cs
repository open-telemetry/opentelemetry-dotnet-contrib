// <copyright file="OperationName.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.Quartz;

/// <summary>
/// Quartz diagnostic source operation name constants.
/// </summary>
public static class OperationName
{
    /// <summary>
    /// Quartz Job category constants.
    /// </summary>
#pragma warning disable CA1034 // Nested types should not be visible
    public static class Job
#pragma warning restore CA1034 // Nested types should not be visible
    {
        /// <summary>
        /// Quartz job execute diagnostic source operation name.
        /// </summary>
        public const string Execute = "Quartz.Job.Execute";

        /// <summary>
        /// Quartz job veto diagnostic source operation name.
        /// </summary>
        public const string Veto = "Quartz.Job.Veto";
    }
}
