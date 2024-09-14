// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from 'src\OpenTelemetry.SemanticConventions\scripts\templates\registry\SemanticConventionsAttributes.cs.j2' </auto-generated>

#nullable enable

#pragma warning disable CS1570 // XML comment has badly formed XML

namespace OpenTelemetry.SemanticConventions;

/// <summary>
/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
/// </summary>
public static class BrowserAttributes
{
    /// <summary>
    /// Array of brand name and version separated by a space
    /// </summary>
    /// <remarks>
    /// This value is intended to be taken from the <a href="https://wicg.github.io/ua-client-hints/#interface">UA client hints API</a> (<c>navigator.userAgentData.brands</c>)
    /// </remarks>
    public const string AttributeBrowserBrands = "browser.brands";

    /// <summary>
    /// Preferred language of the user using the browser
    /// </summary>
    /// <remarks>
    /// This value is intended to be taken from the Navigator API <c>navigator.language</c>
    /// </remarks>
    public const string AttributeBrowserLanguage = "browser.language";

    /// <summary>
    /// A boolean that is true if the browser is running on a mobile device
    /// </summary>
    /// <remarks>
    /// This value is intended to be taken from the <a href="https://wicg.github.io/ua-client-hints/#interface">UA client hints API</a> (<c>navigator.userAgentData.mobile</c>). If unavailable, this attribute SHOULD be left unset
    /// </remarks>
    public const string AttributeBrowserMobile = "browser.mobile";

    /// <summary>
    /// The platform on which the browser is running
    /// </summary>
    /// <remarks>
    /// This value is intended to be taken from the <a href="https://wicg.github.io/ua-client-hints/#interface">UA client hints API</a> (<c>navigator.userAgentData.platform</c>). If unavailable, the legacy <c>navigator.platform</c> API SHOULD NOT be used instead and this attribute SHOULD be left unset in order for the values to be consistent.
    /// The list of possible values is defined in the <a href="https://wicg.github.io/ua-client-hints/#sec-ch-ua-platform">W3C User-Agent Client Hints specification</a>. Note that some (but not all) of these values can overlap with values in the <a href="./os.md"><c>os.type</c> and <c>os.name</c> attributes</a>. However, for consistency, the values in the <c>browser.platform</c> attribute should capture the exact value that the user agent provides
    /// </remarks>
    public const string AttributeBrowserPlatform = "browser.platform";
}
