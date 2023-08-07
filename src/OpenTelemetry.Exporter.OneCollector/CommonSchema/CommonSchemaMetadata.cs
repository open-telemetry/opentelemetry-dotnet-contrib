// <copyright file="CommonSchemaMetadata.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OneCollector;

public interface ICommonSchemaMetadataProvider
{
    ICommonSchemaMetadataFieldDefinitionEnumerator GetCommonSchemaMetadataFieldDefinitionEnumerator();
}

public interface ICommonSchemaMetadataFieldDefinitionEnumerator
{
    ref readonly CommonSchemaMetadataFieldDefinition Current { get; }

    bool MoveNext();
}

public readonly record struct CommonSchemaMetadataFieldDefinition
{
    /*public static void CreateMapField(string name, IReadOnlyList<CommonSchemaFieldMetadata> mapFieldMetadata, out CommonSchemaFieldMetadata fieldDefinition)
    {
        Guard.ThrowIfNull(mapFieldMetadata);

        fieldDefinition = new(0UL, name)
        {
            IsMap = true,
            Fields = mapFieldMetadata,
        };
    }

    public static void CreateArrayField(string name, CommonSchemaFieldMetadata arrayItemMetadata, out CommonSchemaFieldMetadata fieldDefinition)
    {
        Guard.ThrowIfNull(arrayItemMetadata);

        fieldDefinition = new(0UL, name)
        {
            IsArray = true,
            Fields = new[] { arrayItemMetadata },
        };
    }*/

    public ulong TypeInfo { get; }

    public string Name { get; }

    /*public IReadOnlyList<CommonSchemaFieldMetadata>? Fields { get; init; }

    public bool IsArray { get; init; }

    public bool IsMap { get; init; }*/

    public CommonSchemaMetadataFieldDataType DataType
        => (CommonSchemaMetadataFieldDataType)(this.TypeInfo & 0x1F);

    public CommonSchemaMetadataFieldDataType ClassificationType
        => (CommonSchemaMetadataFieldDataType)((this.TypeInfo & 0x1FE0) >> 5);

    public CommonSchemaMetadataFieldDefinition(string name)
        : this(0UL, name)
    {
    }

    public CommonSchemaMetadataFieldDefinition(CommonSchemaMetadataFieldDataType dataType, string name)
        : this(BuildTypeInfo(dataType, CommonSchemaMetadataFieldPrivacyClassificationType.NotSet), name)
    {
    }

    public CommonSchemaMetadataFieldDefinition(
        CommonSchemaMetadataFieldDataType dataType,
        CommonSchemaMetadataFieldPrivacyClassificationType classificationType,
        string name)
        : this(BuildTypeInfo(dataType, classificationType), name)
    {
    }

    private CommonSchemaMetadataFieldDefinition(ulong typeInfo, string name)
    {
        Guard.ThrowIfNullOrWhitespace(name);

        this.TypeInfo = typeInfo;
        this.Name = name;
    }

    private static ulong BuildTypeInfo(
        CommonSchemaMetadataFieldDataType dataType,
        CommonSchemaMetadataFieldPrivacyClassificationType classificationType)
    {
        return (ulong)dataType | (ulong)classificationType << 5;
    }
}

public enum CommonSchemaMetadataFieldDataType
{
    NotSet = 0,
    String = 1,
    Int32 = 2,
    UInt32 = 3,
    Int64 = 4,
    UInt64 = 5,
    Double = 6,
    Bool = 7,
    Guid = 8,
    DateTime = 9,
}

public enum CommonSchemaMetadataFieldPrivacyClassificationType
{
    NotSet = 0,
    DistinguishedName = 1,
    GenericData = 2,
    IpV4Address = 3,
    IpV6Address = 4,
    MailSubject = 5,
    PhoneNumber = 6,
    QueryString = 7,
    SipAddress = 8,
    SmtpAddress = 9,
    Identity = 10,
    Uri = 11,
    FullyQualifiedDomainName = 12,
    IpV4AddressLegacy = 13,
}
