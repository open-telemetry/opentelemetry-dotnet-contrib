// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal;

/// <summary>
/// A query string made of parameters whose values all contain a secret.
/// </summary>
public sealed class SecretQuery(string query)
{
    public const string Secret = "s3cr3t-v4lu3";

    public string Query { get; } = query;

    public override string ToString() => $"\"{this.Query}\"";
}
