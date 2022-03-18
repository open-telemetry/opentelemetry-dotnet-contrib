// <copyright file="Extensions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.GrpcCore
{
    using System;

    /// <summary>
    /// Other useful extensions.
    /// </summary>
    internal static class Extensions
    {
        /// <summary>
        /// Builds an Action comprised of two calls to Dispose with best effort execution for the second disposable.
        /// </summary>
        /// <param name="first">The first.</param>
        /// <param name="second">The second.</param>
        /// <returns>An Action.</returns>
        internal static Action WithBestEffortDispose(this IDisposable first, IDisposable second)
        {
            return () =>
            {
                try
                {
                    first.Dispose();
                }
                finally
                {
                    second.Dispose();
                }
            };
        }
    }
}
