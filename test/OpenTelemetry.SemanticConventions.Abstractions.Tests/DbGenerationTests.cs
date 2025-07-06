// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace OpenTelemetry.SemanticConventions.Abstractions.Tests;

[UsesVerify]
public class DbGenerationTests
{
    [Fact]
    public Task Constructor()
    {
        // The source code to test

        var sb = new StringBuilder();
        sb.Append(@"
using OpenTelemetry.SemanticConventions;

namespace OpenTelemetry.SemanticConventions;

[Random]
[Boo(2)]
[OtelAttributeNamespace(")
            .Append("\"Db\"")
            .Append(@")]
public partial struct Db
{
}");

        var source = sb.ToString();

        // Pass the source code to our helper and snapshot test the output
        return TestHelper.Verify(source);
    }

    [Fact]
    public Task NamedEqual()
    {
        // The source code to test

        var sb = new StringBuilder();
        sb.Append(@"
using OpenTelemetry.SemanticConventions;

namespace OpenTelemetry.SemanticConventions.Example;

[Random]
[Boo(2)]
[OtelAttributeNamespace(Namespace = ")
            .Append("\"Db\"")
            .Append(@")]
internal partial struct DbAttributeNames
    {
    }");

        var source = sb.ToString();

        // Pass the source code to our helper and snapshot test the output
        return TestHelper.Verify(source);
    }
}
