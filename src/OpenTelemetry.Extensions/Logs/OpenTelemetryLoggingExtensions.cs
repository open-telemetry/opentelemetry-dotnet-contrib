// <copyright file="OpenTelemetryLoggingExtensions.cs" company="OpenTelemetry Authors">
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

#if NET462_OR_GREATER || NETSTANDARD2_0 || NET6_0_OR_GREATER
using System;
using System.Diagnostics;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace Microsoft.Extensions.Logging;

/// <summary>
/// Contains OpenTelemetry logging SDK extensions.
/// </summary>
public static class OpenTelemetryLoggingExtensions
{
    /// <summary>
    /// Adds a <see cref="LogRecord"/> processor to the OpenTelemetry <see
    /// cref="OpenTelemetryLoggerOptions"/> which will convert log messages
    /// into <see cref="ActivityEvent"/>s attached to the active <see
    /// cref="Activity"/> when the message is written.
    /// </summary>
    /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
    /// <param name="configure"><see cref="LogToActivityEventConversionOptions"/>.</param>
    /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="loggerOptions"/> is <c>null</c>.</exception>
    public static OpenTelemetryLoggerOptions AttachLogsToActivityEvent(
        this OpenTelemetryLoggerOptions loggerOptions,
        Action<LogToActivityEventConversionOptions>? configure = null)
    {
        Guard.ThrowIfNull(loggerOptions);

        var options = new LogToActivityEventConversionOptions();
        configure?.Invoke(options);
#pragma warning disable CA2000 // Dispose objects before losing scope
        return loggerOptions.AddProcessor(new ActivityEventAttachingLogProcessor(options));
#pragma warning restore CA2000 // Dispose objects before losing scope
    }
}
#endif
