// <copyright file="CommonSchemaMetadataFieldPrivacyClassificationType.cs" company="OpenTelemetry Authors">
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

/// <summary>
/// Describes the Common Schema field privacy classification type.
/// </summary>
public enum CommonSchemaMetadataFieldPrivacyClassificationType
{
    /// <summary>
    /// Privacy classification type is not set.
    /// </summary>
    NotSet = 0,

    /// <summary>
    /// Distinguished name privacy classification type.
    /// </summary>
    DistinguishedName = 1,

    /// <summary>
    /// Generic data privacy classification type.
    /// </summary>
    GenericData = 2,

    /// <summary>
    /// IPv4 privacy classification type.
    /// </summary>
    IpV4Address = 3,

    /// <summary>
    /// IPv6 privacy classification type.
    /// </summary>
    IpV6Address = 4,

    /// <summary>
    /// Mail subject privacy classification type.
    /// </summary>
    MailSubject = 5,

    /// <summary>
    /// Phone number privacy classification type.
    /// </summary>
    PhoneNumber = 6,

    /// <summary>
    /// Query string privacy classification type.
    /// </summary>
    QueryString = 7,

    /// <summary>
    /// SIP address privacy classification type.
    /// </summary>
    SipAddress = 8,

    /// <summary>
    /// SMTP address privacy classification type.
    /// </summary>
    SmtpAddress = 9,

    /// <summary>
    /// Identity privacy classification type.
    /// </summary>
    Identity = 10,

    /// <summary>
    /// URI privacy classification type.
    /// </summary>
    Uri = 11,

    /// <summary>
    /// Fully qualified domain name privacy classification type.
    /// </summary>
    FullyQualifiedDomainName = 12,

    /// <summary>
    /// IPv4 legacy privacy classification type.
    /// </summary>
    IpV4AddressLegacy = 13,
}
