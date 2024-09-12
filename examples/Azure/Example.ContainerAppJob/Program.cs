// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Example.ContainerAppJob;

var builder = Host.CreateApplicationBuilder(args);

builder.ConfigureOpenTelemetry();

builder.Services.AddHttpClient();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
