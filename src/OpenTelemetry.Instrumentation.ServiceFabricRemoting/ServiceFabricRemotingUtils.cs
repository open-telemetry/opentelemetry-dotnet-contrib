// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting;

internal static class ServiceFabricRemotingUtils
{
    internal static void InjectTraceContextIntoServiceRemotingRequestMessageHeader(IServiceRemotingRequestMessageHeader requestMessageHeader, string key, string value)
    {
        if (!requestMessageHeader.TryGetHeaderValue(key, out byte[] _))
        {
            byte[] valueAsBytes = Encoding.UTF8.GetBytes(value);

            requestMessageHeader.AddHeader(key, valueAsBytes);
        }
    }

    internal static IEnumerable<string> ExtractTraceContextFromRequestMessageHeader(IServiceRemotingRequestMessageHeader requestMessageHeader, string headerKey)
    {
        if (requestMessageHeader.TryGetHeaderValue(headerKey, out byte[] headerValueAsBytes))
        {
            string headerValue = Encoding.UTF8.GetString(headerValueAsBytes);

            return [headerValue];
        }

        return [];
    }

    // Returns the SF service URI suitable for the server.address metric tag, or null
    // if it is not available. ResolvedServicePartition can be null or its getter can throw
    // in some adapter states (including test doubles), so failures are swallowed.
    internal static string? GetServerAddress(IServiceRemotingClient client)
    {
        try
        {
            return client.ResolvedServicePartition?.ServiceName?.AbsoluteUri;
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static double CalculateDurationFromTimestamp(long begin)
    {
#if NET
        TimeSpan duration = Stopwatch.GetElapsedTime(begin);
#else
        long end = Stopwatch.GetTimestamp();
        double timestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;
        long delta = end - begin;
        long ticks = (long)(timestampToTicks * delta);
        TimeSpan duration = new TimeSpan(ticks);
#endif

        return duration.TotalSeconds;
    }
}
