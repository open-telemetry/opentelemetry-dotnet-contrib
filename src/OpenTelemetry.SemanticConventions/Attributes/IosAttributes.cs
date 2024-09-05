// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from 'src\OpenTelemetry.SemanticConventions\scripts\templates\registry\SemanticConventionsAttributes.cs.j2' </auto-generated>

#pragma warning disable CS1570 // XML comment has badly formed XML

using System;

namespace OpenTelemetry.SemanticConventions;

/// <summary>
/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
/// </summary>
public static class IosAttributes
{
    /// <summary>
    /// Deprecated use the <c>device.app.lifecycle</c> event definition including <c>ios.state</c> as a payload field instead
    /// </summary>
    /// <remarks>
    /// The iOS lifecycle states are defined in the <a href="https://developer.apple.com/documentation/uikit/uiapplicationdelegate#1656902">UIApplicationDelegate documentation</a>, and from which the <c>OS terminology</c> column values are derived
    /// </remarks>
    [Obsolete("Moved to a payload field of <c>device.app.lifecycle</c>")]
    public const string AttributeIosState = "ios.state";

    /// <summary>
    /// Deprecated use the <c>device.app.lifecycle</c> event definition including <c>ios.state</c> as a payload field instead
    /// </summary>
    public static class IosStateValues
    {
        /// <summary>
        /// The app has become <c>active</c>. Associated with UIKit notification <c>applicationDidBecomeActive</c>
        /// </summary>
        [Obsolete("Moved to a payload field of <c>device.app.lifecycle</c>")]
        public const string Active = "active";

        /// <summary>
        /// The app is now <c>inactive</c>. Associated with UIKit notification <c>applicationWillResignActive</c>
        /// </summary>
        [Obsolete("Moved to a payload field of <c>device.app.lifecycle</c>")]
        public const string Inactive = "inactive";

        /// <summary>
        /// The app is now in the background. This value is associated with UIKit notification <c>applicationDidEnterBackground</c>
        /// </summary>
        [Obsolete("Moved to a payload field of <c>device.app.lifecycle</c>")]
        public const string Background = "background";

        /// <summary>
        /// The app is now in the foreground. This value is associated with UIKit notification <c>applicationWillEnterForeground</c>
        /// </summary>
        [Obsolete("Moved to a payload field of <c>device.app.lifecycle</c>")]
        public const string Foreground = "foreground";

        /// <summary>
        /// The app is about to terminate. Associated with UIKit notification <c>applicationWillTerminate</c>
        /// </summary>
        [Obsolete("Moved to a payload field of <c>device.app.lifecycle</c>")]
        public const string Terminate = "terminate";
    }
}
