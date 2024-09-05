// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from 'src\OpenTelemetry.SemanticConventions\scripts\templates\registry\SemanticConventionsAttributes.cs.j2' </auto-generated>

#pragma warning disable CS1570 // XML comment has badly formed XML

using System;

namespace OpenTelemetry.SemanticConventions;

/// <summary>
/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
/// </summary>
public static class FileAttributes
{
    /// <summary>
    /// Directory where the file is located. It should include the drive letter, when appropriate
    /// </summary>
    public const string AttributeFileDirectory = "file.directory";

    /// <summary>
    /// File extension, excluding the leading dot
    /// </summary>
    /// <remarks>
    /// When the file name has multiple extensions (example.tar.gz), only the last one should be captured ("gz", not "tar.gz")
    /// </remarks>
    public const string AttributeFileExtension = "file.extension";

    /// <summary>
    /// Name of the file including the extension, without the directory
    /// </summary>
    public const string AttributeFileName = "file.name";

    /// <summary>
    /// Full path to the file, including the file name. It should include the drive letter, when appropriate
    /// </summary>
    public const string AttributeFilePath = "file.path";

    /// <summary>
    /// File size in bytes
    /// </summary>
    public const string AttributeFileSize = "file.size";
}
