// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Microsoft.ServiceFabric.Services.Remoting.V2;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting;

internal static class ServiceFabricRemotingUtils
{
    /// <summary>
    /// The name of the configuration section that Service Fabric reads the default transport settings,
    /// including the transport security credentials, from.
    /// </summary>
    internal const string DefaultTransportSettingsSectionName = "TransportSettings";

    /// <summary>
    /// Invokes a loader for Service Fabric transport settings, returning <see langword="null"/>
    /// if the native Service Fabric runtime that loading settings from configuration requires is not available.
    /// </summary>
    /// <typeparam name="T">The type of the settings.</typeparam>
    /// <param name="loader">The delegate that loads the settings.</param>
    /// <returns>The loaded settings, or <see langword="null"/> if no settings could be loaded.</returns>
    internal static T? TryLoadTransportSettings<T>(Func<T?> loader)
        where T : class
    {
        try
        {
            return loader();
        }
        catch (DllNotFoundException)
        {
            return null;
        }
        catch (TypeInitializationException ex) when (ex.InnerException is DllNotFoundException)
        {
            return null;
        }
    }

    internal static void InjectTraceContextIntoServiceRemotingRequestMessageHeader(IServiceRemotingRequestMessageHeader requestMessageHeader, string key, string value)
    {
        if (!requestMessageHeader.TryGetHeaderValue(key, out var _))
        {
            var valueAsBytes = Encoding.UTF8.GetBytes(value);

            requestMessageHeader.AddHeader(key, valueAsBytes);
        }
    }

    internal static IEnumerable<string> ExtractTraceContextFromRequestMessageHeader(IServiceRemotingRequestMessageHeader requestMessageHeader, string headerKey)
    {
        if (requestMessageHeader.TryGetHeaderValue(headerKey, out var headerValueAsBytes))
        {
            var headerValue = Encoding.UTF8.GetString(headerValueAsBytes);

            return [headerValue];
        }

        return [];
    }
}
