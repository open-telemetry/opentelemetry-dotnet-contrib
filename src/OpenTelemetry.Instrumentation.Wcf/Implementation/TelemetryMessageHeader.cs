// <copyright file="TelemetryMessageHeader.cs" company="OpenTelemetry Authors">
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

using System.ServiceModel.Channels;
using System.Xml;

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

internal class TelemetryMessageHeader : MessageHeader
{
    private const string NAMESPACE = "https://www.w3.org/TR/trace-context/";
    private string name;
    private string value;

    private TelemetryMessageHeader(string name, string value)
    {
        this.name = name;
        this.value = value;
    }

    public override string Name => this.name;

    public string Value => this.value;

    public override string Namespace => NAMESPACE;

    public static TelemetryMessageHeader CreateHeader(string name, string value)
    {
        return new TelemetryMessageHeader(name, value);
    }

    public static TelemetryMessageHeader FindHeader(string name, MessageHeaders allHeaders)
    {
        try
        {
            var headerIndex = allHeaders.FindHeader(name, NAMESPACE);
            if (headerIndex < 0)
            {
                return null;
            }

            using var reader = allHeaders.GetReaderAtHeader(headerIndex);
            reader.Read();
            return new TelemetryMessageHeader(name, reader.ReadContentAsString());
        }
        catch (XmlException)
        {
            return null;
        }
    }

    protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
    {
        writer.WriteString(this.value);
    }
}
