// <copyright file="OneCollectorExporterJsonSerializationOptions.cs" company="OpenTelemetry Authors">
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

using System.Text.Json;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OneCollector;

/// <summary>
/// Contains JSON serialization options for the <see cref="OneCollectorExporter{T}"/> class.
/// </summary>
public sealed class OneCollectorExporterJsonSerializationOptions
{
    private bool isReadOnly;

    internal Dictionary<Type, Action<object, Utf8JsonWriter>> Formatters { get; } = new();

    /// <summary>
    /// Registers a callback used to format <typeparamref name="T"/> instances
    /// into JSON using <see cref="Utf8JsonWriter"/>.
    /// </summary>
    /// <typeparam name="T">Type.</typeparam>
    /// <param name="formatter">Formatter callback.</param>
    /// <returns>The supplied <see
    /// cref="OneCollectorExporterJsonSerializationOptions"/> instance for call
    /// chaining.</returns>
    public OneCollectorExporterJsonSerializationOptions RegisterFormatter<T>(Action<T, Utf8JsonWriter> formatter)
    {
        Guard.ThrowIfNull(formatter);

        if (this.isReadOnly)
        {
            throw new NotSupportedException("Formatter registration is not supported after options have been made read-only.");
        }

        this.Formatters[typeof(T)]
            = (o, w) => formatter((T)o, w);

        return this;
    }

    internal OneCollectorExporterJsonSerializationOptions MakeReadOnly()
    {
        this.isReadOnly = true;
        return this;
    }

#pragma warning disable CA1822 // Mark members as static
    internal void Validate()
#pragma warning restore CA1822 // Mark members as static
    {
    }
}
