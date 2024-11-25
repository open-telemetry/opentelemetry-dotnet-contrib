// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Examples.GrpcCore.AspNetCore;

#pragma warning disable CA1515
public class WeatherForecast
#pragma warning restore CA1515
{
    public WeatherForecast(DateTime date, int temperatureC, string summary)
    {
        this.Date = date;
        this.TemperatureC = temperatureC;
        this.Summary = summary;
    }

    public DateTime Date { get; }

    public int TemperatureC { get; }

    public string Summary { get; }
}
