// <copyright file="EventNameExportMode.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Geneva;

[Flags]
public enum EventNameExportMode
{
    /// <summary>
    /// GenevaExporter will not export <a href="https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.eventid.name">eventId.Name</a>
    /// if the enum instance has no other flag selected.
    /// </summary>
    None = 0,

    /// <summary>
    /// GenevaExporter will export <a href="https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.eventid.name">eventId.Name</a>
    /// as Part A name field when this flag is selected.
    /// </summary>
    ExportAsPartAName = 1,

    /* Note: This might be added in future.
    /// <summary>
    /// GenevaExporter will export <a href="https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.eventid.name">eventId.Name</a>
    /// as the table name when this flag is selected.
    /// </summary>
    ExportAsTableName = 2,
    */
}
