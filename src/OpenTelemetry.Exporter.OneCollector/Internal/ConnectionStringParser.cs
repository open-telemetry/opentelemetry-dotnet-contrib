// <copyright file="ConnectionStringParser.cs" company="OpenTelemetry Authors">
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

internal sealed class ConnectionStringParser
{
    public ConnectionStringParser(string connectionString)
    {
        Guard.ThrowIfNullOrWhitespace(connectionString);

        const char Semicolon = ';';
        const char EqualSign = '=';

        foreach (var token in connectionString.Split(Semicolon))
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            var index = token.IndexOf(EqualSign, StringComparison.Ordinal);
#else
            var index = token.IndexOf(EqualSign);
#endif
            if (index == -1 || index != token.LastIndexOf(EqualSign))
            {
                continue;
            }

            var pair = token.Trim().Split(EqualSign);

            var key = pair[0].Trim();
            var value = pair[1].Trim();
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Connection string cannot contain empty keys or values.", nameof(connectionString));
            }

            this.ParsedKeyValues[key] = value;
        }

        if (this.ParsedKeyValues.Count == 0)
        {
            throw new ArgumentException("Connection string is invalid.", nameof(connectionString));
        }
    }

    public string? InstrumentationKey
    {
        get
        {
            this.ParsedKeyValues.TryGetValue(nameof(this.InstrumentationKey), out string? instrumentationKey);

            return instrumentationKey;
        }
    }

    internal Dictionary<string, string> ParsedKeyValues { get; } = new(StringComparer.Ordinal);
}
