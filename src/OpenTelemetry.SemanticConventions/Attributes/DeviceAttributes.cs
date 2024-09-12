// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from 'src\OpenTelemetry.SemanticConventions\scripts\templates\registry\SemanticConventionsAttributes.cs.j2' </auto-generated>

#pragma warning disable CS1570 // XML comment has badly formed XML

namespace OpenTelemetry.SemanticConventions;

/// <summary>
/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
/// </summary>
public static class DeviceAttributes
{
    /// <summary>
    /// A unique identifier representing the device
    /// </summary>
    /// <remarks>
    /// The device identifier MUST only be defined using the values outlined below. This value is not an advertising identifier and MUST NOT be used as such. On iOS (Swift or Objective-C), this value MUST be equal to the <a href="https://developer.apple.com/documentation/uikit/uidevice/1620059-identifierforvendor">vendor identifier</a>. On Android (Java or Kotlin), this value MUST be equal to the Firebase Installation ID or a globally unique UUID which is persisted across sessions in your application. More information can be found <a href="https://developer.android.com/training/articles/user-data-ids">here</a> on best practices and exact implementation details. Caution should be taken when storing personal data or anything which can identify a user. GDPR and data protection laws may apply, ensure you do your own due diligence
    /// </remarks>
    public const string AttributeDeviceId = "device.id";

    /// <summary>
    /// The name of the device manufacturer
    /// </summary>
    /// <remarks>
    /// The Android OS provides this field via <a href="https://developer.android.com/reference/android/os/Build#MANUFACTURER">Build</a>. iOS apps SHOULD hardcode the value <c>Apple</c>
    /// </remarks>
    public const string AttributeDeviceManufacturer = "device.manufacturer";

    /// <summary>
    /// The model identifier for the device
    /// </summary>
    /// <remarks>
    /// It's recommended this value represents a machine-readable version of the model identifier rather than the market or consumer-friendly name of the device
    /// </remarks>
    public const string AttributeDeviceModelIdentifier = "device.model.identifier";

    /// <summary>
    /// The marketing name for the device model
    /// </summary>
    /// <remarks>
    /// It's recommended this value represents a human-readable version of the device model rather than a machine-readable alternative
    /// </remarks>
    public const string AttributeDeviceModelName = "device.model.name";
}
