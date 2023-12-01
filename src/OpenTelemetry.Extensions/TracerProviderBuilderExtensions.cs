// <copyright file="TracerProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using System.Threading;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of auto flush Activity processor.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Adds auto flush Activity processor.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="predicate">Predicate that should return <c>true</c> to initiate a flush.</param>
    /// <param name="timeoutMilliseconds">Timeout (in milliseconds) to use for flushing. Specify <see cref="Timeout.Infinite"/>
    /// to wait indefinitely.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when the <c>builder</c> is null.</exception>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    /// <remarks>
    /// Add this processor *after* exporter related Activity processors.
    /// It's assumed that the predicate is defined as a lambda expression which is executed quite fast and
    /// doesn't contain more complex code. The predicate must not create new Activity instances,
    /// otherwise the behavior is undefined. Any exception thrown by the predicate will be swallowed and logged.
    /// In case of an exception the predicate is treated as false which means flush will not be applied.
    /// </remarks>
    public static TracerProviderBuilder AddAutoFlushActivityProcessor(
        this TracerProviderBuilder builder,
        Func<Activity, bool> predicate,
        int timeoutMilliseconds = 10000)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(builder);
#else
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
#endif

#pragma warning disable CA2000 // Dispose objects before losing scope
        return builder.AddProcessor(new AutoFlushActivityProcessor(predicate, timeoutMilliseconds));
#pragma warning restore CA2000 // Dispose objects before losing scope
    }
}
