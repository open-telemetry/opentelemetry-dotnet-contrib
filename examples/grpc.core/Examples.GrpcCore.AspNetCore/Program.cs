// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Examples.GrpcCore.AspNetCore;

public class Program
{
    internal const int Port = 5000;
    internal const int GrpcServicePort = 5001;

    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls($"http://+:{Port}");
                webBuilder.UseStartup<Startup>();
            });
}
