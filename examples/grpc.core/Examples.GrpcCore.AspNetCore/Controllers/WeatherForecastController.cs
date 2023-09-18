// <copyright file="WeatherForecastController.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Examples.GrpcCore.AspNetCore.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching",
    };

    private readonly Echo.EchoClient echoClient;

    public WeatherForecastController(Echo.EchoClient echoClient)
    {
        this.echoClient = echoClient;
    }

    [HttpGet]
    public async Task<IEnumerable<WeatherForecast>> Get()
    {
        var echoCall = this.echoClient.EchoAsync(new EchoRequest { Message = "Hello" });
        var echoResponse = await echoCall.ResponseAsync.ConfigureAwait(false);

        var rng = new Random();
        return Enumerable.Range(1, 5).Select(index => new WeatherForecast(
                date: DateTime.Now.AddDays(index),
                temperatureC: rng.Next(-20, 55),
                summary: Summaries[rng.Next(Summaries.Length)]))
            .ToArray();
    }
}
