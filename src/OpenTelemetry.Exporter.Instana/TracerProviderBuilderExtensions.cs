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
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Instana;

/// <summary>
/// Extension methods for <see cref="TracerProviderBuilder"/> for using Instana.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Instana tracer provider builder extension.
    /// </summary>
    /// <param name="options">Tracer provider builder.</param>
    /// <returns>todo.</returns>
    /// <exception cref="ArgumentNullException">Tracer provider builder is null.</exception>
    public static TracerProviderBuilder AddInstanaExporter(this TracerProviderBuilder options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

#pragma warning disable CA2000
        return options.AddProcessor(new BatchActivityExportProcessor(new InstanaExporter()));
#pragma warning restore CA2000
    }
}
