// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from 'src\OpenTelemetry.SemanticConventions\scripts\templates\registry\SemanticConventionsAttributes.cs.j2' </auto-generated>

#pragma warning disable CS1570 // XML comment has badly formed XML

using System;

namespace OpenTelemetry.SemanticConventions;

/// <summary>
/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
/// </summary>
public static class AndroidAttributes
{
    /// <summary>
    /// Uniquely identifies the framework API revision offered by a version (<c>os.version</c>) of the android operating system. More information can be found <a href="https://developer.android.com/guide/topics/manifest/uses-sdk-element#ApiLevels">here</a>
    /// </summary>
    public const string AttributeAndroidOsApiLevel = "android.os.api_level";

    /// <summary>
    /// Deprecated use the <c>device.app.lifecycle</c> event definition including <c>android.state</c> as a payload field instead
    /// </summary>
    /// <remarks>
    /// The Android lifecycle states are defined in <a href="https://developer.android.com/guide/components/activities/activity-lifecycle#lc">Activity lifecycle callbacks</a>, and from which the <c>OS identifiers</c> are derived
    /// </remarks>
    public const string AttributeAndroidState = "android.state";

    /// <summary>
    /// Deprecated use the <c>device.app.lifecycle</c> event definition including <c>android.state</c> as a payload field instead
    /// </summary>
    public static class AndroidStateValues
    {
        /// <summary>
        /// Any time before Activity.onResume() or, if the app has no Activity, Context.startService() has been called in the app for the first time
        /// </summary>
        public const string Created = "created";

        /// <summary>
        /// Any time after Activity.onPause() or, if the app has no Activity, Context.stopService() has been called when the app was in the foreground state
        /// </summary>
        public const string Background = "background";

        /// <summary>
        /// Any time after Activity.onResume() or, if the app has no Activity, Context.startService() has been called when the app was in either the created or background states
        /// </summary>
        public const string Foreground = "foreground";
    }
}
