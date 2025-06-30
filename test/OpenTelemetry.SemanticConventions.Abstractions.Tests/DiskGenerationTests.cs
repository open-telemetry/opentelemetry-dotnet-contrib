// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace OpenTelemetry.SemanticConventions.Abstractions.Tests;

[UsesVerify]
public class DiskGenerationTests
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
            .Append("\"Disk\"")
            .Append(@")]
public partial struct Disk
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
[OtelAttributeNamespace(AttributeNamespace = ")
            .Append("\"Disk\"")
            .Append(@")]
internal partial struct DiskAttributeNames
    {
    }");

        var source = sb.ToString();

        // Pass the source code to our helper and snapshot test the output
        return TestHelper.Verify(source);
    }
}
