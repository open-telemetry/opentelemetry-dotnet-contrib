// <copyright file="CommonSchemaMetadataFieldDataType.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.OneCollector;

#pragma warning disable CA1720 // Identifier contains type name

/// <summary>
/// Describes the Common Schema field metadata type.
/// </summary>
public enum CommonSchemaMetadataFieldDataType
{
    /// <summary>
    /// Data type is not set.
    /// </summary>
    NotSet = 0,

    /// <summary>
    /// String data type.
    /// </summary>
    String = 1,

    /// <summary>
    /// Int32 data type.
    /// </summary>
    Int32 = 2,

    /// <summary>
    /// Int32 data type.
    /// </summary>
    UInt32 = 3,

    /// <summary>
    /// Int64 data type.
    /// </summary>
    Int64 = 4,

    /// <summary>
    /// UInt64 data type.
    /// </summary>
    UInt64 = 5,

    /// <summary>
    /// Double data type.
    /// </summary>
    Double = 6,

    /// <summary>
    /// Bool data type.
    /// </summary>
    Bool = 7,

    /// <summary>
    /// Guid data type.
    /// </summary>
    Guid = 8,

    /// <summary>
    /// DateTime data type.
    /// </summary>
    DateTime = 9,
}
