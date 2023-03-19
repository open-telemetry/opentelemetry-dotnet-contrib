// <copyright file="OneCollectorExporterValidationException.cs" company="OpenTelemetry Authors">
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

using System.Runtime.Serialization;

namespace OpenTelemetry.Exporter.OneCollector;

/// <summary>
/// Represents errors that occur validating OneCollectorExporter configuration.
/// </summary>
[Serializable]
public sealed class OneCollectorExporterValidationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OneCollectorExporterValidationException"/> class.
    /// </summary>
    public OneCollectorExporterValidationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OneCollectorExporterValidationException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public OneCollectorExporterValidationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see
    /// cref="OneCollectorExporterValidationException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the
    /// exception.</param>
    /// <param name="innerException">The exception that is the cause of the
    /// current exception, or a <see langword="null"/> reference if no inner
    /// exception is specified.</param>
    public OneCollectorExporterValidationException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    private OneCollectorExporterValidationException(SerializationInfo serializationInfo, StreamingContext streamingContext)
        : base(serializationInfo, streamingContext)
    {
    }
}
