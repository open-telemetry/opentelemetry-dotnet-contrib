// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using Xunit;

namespace OpenTelemetry.SemanticConventions.Tests;

public class GenerationScriptPinningTests
{
    [Fact]
    public void BashAndPowerShellScriptsPinSameSemanticConventionsAndWeaverVersions()
    {
        var repoRoot = FindRepoRoot();
        var bashScript = File.ReadAllText(Path.Combine(repoRoot, "src", "OpenTelemetry.SemanticConventions", "scripts", "generate.sh"));
        var powershellScript = File.ReadAllText(Path.Combine(repoRoot, "src", "OpenTelemetry.SemanticConventions", "scripts", "generate.ps1"));

        Assert.Equal("1.41.0", ExtractBashVariable(bashScript, "SEMCONV_VERSION"));
        Assert.Equal("1.41.0", ExtractPowerShellVariable(powershellScript, "SEMCONV_VERSION"));
        Assert.Equal(ExtractBashVariable(bashScript, "SEMCONV_VERSION"), ExtractPowerShellVariable(powershellScript, "SEMCONV_VERSION"));

        Assert.Equal("v0.23.0", ExtractBashVariable(bashScript, "GENERATOR_VERSION"));
        Assert.Equal("v0.23.0", ExtractPowerShellVariable(powershellScript, "GENERATOR_VERSION"));
        Assert.Equal(ExtractBashVariable(bashScript, "GENERATOR_VERSION"), ExtractPowerShellVariable(powershellScript, "GENERATOR_VERSION"));

        Assert.Equal("e018fe6f91862f5ed63c082f87697cddac596784", ExtractBashVariable(bashScript, "SEMCONV_COMMIT"));
        Assert.Equal("e018fe6f91862f5ed63c082f87697cddac596784", ExtractPowerShellVariable(powershellScript, "SEMCONV_COMMIT"));
        Assert.Equal(ExtractBashVariable(bashScript, "SEMCONV_COMMIT"), ExtractPowerShellVariable(powershellScript, "SEMCONV_COMMIT"));
    }

    private static string ExtractBashVariable(string script, string name)
    {
        var match = Regex.Match(script, $@"^{name}=""(?<value>[^""]+)""", RegexOptions.Multiline);
        Assert.True(match.Success, $"Could not find {name} in generate.sh.");
        return match.Groups["value"].Value;
    }

    private static string ExtractPowerShellVariable(string script, string name)
    {
        var match = Regex.Match(script, $@"^\${name}=""(?<value>[^""]+)""", RegexOptions.Multiline);
        Assert.True(match.Success, $"Could not find {name} in generate.ps1.");
        return match.Groups["value"].Value;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "opentelemetry-dotnet-contrib.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
