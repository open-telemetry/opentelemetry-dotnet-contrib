// <copyright file="EntityFrameworkDiagnosticListenerTests.cs" company="OpenTelemetry Authors">
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

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Tests;

public class EnvironmentVariablesOptionsTests
{
    [Fact]
    public void TestEnvVariablesTrue()
    {
        Environment.SetEnvironmentVariable(EntityFrameworkConstants.OTEL_DOTNET_EF_SET_DB_STATEMENT_STORED_PROCEDURE, "true");
        Environment.SetEnvironmentVariable(EntityFrameworkConstants.OTEL_DOTNET_EF_SET_DB_STATEMENT_TEXT, "true");

        var services = new ServiceCollection();

        services
            .AddOpenTelemetry()
            .WithTracing(builder =>
                builder.AddEntityFrameworkCoreInstrumentation());

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptionsMonitor<EntityFrameworkInstrumentationOptions>>().Get(null);

        Assert.True(options.SetDbStatementForStoredProcedure);
        Assert.True(options.SetDbStatementForStoredProcedure);
    }

    [Fact]
    public void TestEnvVariablesFalse()
    {
        Environment.SetEnvironmentVariable(EntityFrameworkConstants.OTEL_DOTNET_EF_SET_DB_STATEMENT_STORED_PROCEDURE, "false");
        Environment.SetEnvironmentVariable(EntityFrameworkConstants.OTEL_DOTNET_EF_SET_DB_STATEMENT_TEXT, "false");

        var services = new ServiceCollection();

        services
            .AddOpenTelemetry()
            .WithTracing(builder =>
                builder.AddEntityFrameworkCoreInstrumentation());

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptionsMonitor<EntityFrameworkInstrumentationOptions>>().Get(null);

        Assert.False(options.SetDbStatementForStoredProcedure);
        Assert.False(options.SetDbStatementForStoredProcedure);
    }

    [Fact]
    public void TestDefaultValues()
    {
        var services = new ServiceCollection();

        services
            .AddOpenTelemetry()
            .WithTracing(builder =>
                builder.AddEntityFrameworkCoreInstrumentation());

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptionsMonitor<EntityFrameworkInstrumentationOptions>>().Get(null);

        Assert.True(options.SetDbStatementForStoredProcedure);
        Assert.False(options.SetDbStatementForText);
    }
}
