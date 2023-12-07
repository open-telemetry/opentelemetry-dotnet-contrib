// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable IDE0005 // Using directive is unnecessary. <- Projects with ImplicitUsings enabled will see warnings on using System

#nullable enable

using System;
using Xunit;

namespace OpenTelemetry.Tests;

internal sealed class SkipUnlessEnvVarFoundTheoryAttribute : TheoryAttribute
{
    public SkipUnlessEnvVarFoundTheoryAttribute(string environmentVariable)
    {
        if (string.IsNullOrEmpty(GetEnvironmentVariable(environmentVariable)))
        {
            this.Skip = $"Skipped because {environmentVariable} environment variable was not configured.";
        }
    }

    public static string? GetEnvironmentVariable(string environmentVariableName)
    {
        var environmentVariableValue = Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Process);

        if (string.IsNullOrEmpty(environmentVariableValue))
        {
            environmentVariableValue = Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Machine);
        }

        return environmentVariableValue;
    }
}
