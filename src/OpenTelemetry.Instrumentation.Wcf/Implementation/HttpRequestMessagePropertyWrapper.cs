// <copyright file="HttpRequestMessagePropertyWrapper.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Net;
using System.Reflection;

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

/// <summary>
/// This is a reflection-based wrapper around the HttpRequestMessageProperty class. It is done this way so we don't need to
/// have an explicit reference to System.ServiceModel.Http.dll. If the consuming application has a reference to
/// System.ServiceModel.Http.dll then the HttpRequestMessageProperty class will be available (IsHttpFunctionalityEnabled == true).
/// If the consuming application does not have a reference to System.ServiceModel.Http.dll then all http-related functionality
/// will be disabled (IsHttpFunctionalityEnabled == false).
/// </summary>
internal static class HttpRequestMessagePropertyWrapper
{
    private static readonly ReflectedInfo ReflectedValues = Initialize();

    public static bool IsHttpFunctionalityEnabled => ReflectedValues != null;

    public static string Name
    {
        get
        {
            AssertHttpEnabled();
            return ReflectedValues.Name;
        }
    }

    public static object CreateNew()
    {
        AssertHttpEnabled();
        return Activator.CreateInstance(ReflectedValues.Type);
    }

    public static WebHeaderCollection GetHeaders(object httpRequestMessageProperty)
    {
        AssertHttpEnabled();
        AssertIsFrameworkMessageProperty(httpRequestMessageProperty);
        return ReflectedValues.HeadersFetcher.Fetch(httpRequestMessageProperty);
    }

    private static ReflectedInfo Initialize()
    {
        Type type = null;
        try
        {
            type = Type.GetType(
                "System.ServiceModel.Channels.HttpRequestMessageProperty, System.ServiceModel, Version=0.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                true);

            var headersProp = type.GetProperty("Headers", BindingFlags.Public | BindingFlags.Instance, null, typeof(WebHeaderCollection), Array.Empty<Type>(), null);
            if (headersProp == null)
            {
                throw new NotSupportedException("HttpRequestMessageProperty.Headers property not found");
            }

            var nameProp = type.GetProperty("Name", BindingFlags.Public | BindingFlags.Static, null, typeof(string), Array.Empty<Type>(), null);
            if (nameProp == null)
            {
                throw new NotSupportedException("HttpRequestMessageProperty.Name property not found");
            }

            return new ReflectedInfo
            {
                Type = type,
                Name = (string)nameProp.GetValue(null),
                HeadersFetcher = new PropertyFetcher<WebHeaderCollection>("Headers"),
            };
        }
        catch (Exception ex)
        {
            WcfInstrumentationEventSource.Log.HttpServiceModelReflectionFailedToBind(ex, type?.Assembly);
        }

        return null;
    }

    [Conditional("DEBUG")]
    private static void AssertHttpEnabled()
    {
        if (!IsHttpFunctionalityEnabled)
        {
            throw new InvalidOperationException("Http functionality is not enabled, check IsHttpFunctionalityEnabled before calling this method");
        }
    }

    [Conditional("DEBUG")]
    private static void AssertIsFrameworkMessageProperty(object httpRequestMessageProperty)
    {
        AssertHttpEnabled();
        if (httpRequestMessageProperty == null || !httpRequestMessageProperty.GetType().Equals(ReflectedValues.Type))
        {
            throw new ArgumentException("Object must be of type HttpRequestMessageProperty");
        }
    }

    private sealed class ReflectedInfo
    {
        public Type Type;
        public string Name;
        public PropertyFetcher<WebHeaderCollection> HeadersFetcher;
    }
}
