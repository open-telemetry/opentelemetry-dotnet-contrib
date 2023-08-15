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
internal class HttpRequestMessagePropertyWrapper
{
    private static readonly ReflectedInfo ReflectedValues = Initialize();
    private readonly object innerObject;

    public HttpRequestMessagePropertyWrapper(object existingHttpRequestMessageProperty = null)
    {
        EnsureEnabled();
        if (existingHttpRequestMessageProperty != null && !existingHttpRequestMessageProperty.GetType().Equals(ReflectedValues.Type))
        {
            throw new ArgumentException("Existing object must be of type HttpRequestMessageProperty", nameof(existingHttpRequestMessageProperty));
        }

        this.innerObject = existingHttpRequestMessageProperty != null ? existingHttpRequestMessageProperty : Activator.CreateInstance(ReflectedValues.Type);
    }

    public static bool IsHttpFunctionalityEnabled => ReflectedValues != null;

    public static string Name
    {
        get
        {
            EnsureEnabled();
            return (string)ReflectedValues.NameProperty.GetValue(null);
        }
    }

    public WebHeaderCollection Headers => (WebHeaderCollection)ReflectedValues.HeadersProperty.GetValue(this.innerObject);

    public object InnerObject => this.innerObject;

    private static ReflectedInfo Initialize()
    {
        try
        {
            var type = Type.GetType(
                "System.ServiceModel.Channels.HttpRequestMessageProperty, System.ServiceModel, Version=0.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                true);
            var headersProp = type.GetProperty("Headers", BindingFlags.Public | BindingFlags.Instance, null, typeof(WebHeaderCollection), Array.Empty<Type>(), null);
            var nameProp = type.GetProperty("Name", BindingFlags.Public | BindingFlags.Static, null, typeof(string), Array.Empty<Type>(), null);

            if (type != null && headersProp != null && nameProp != null)
            {
                return new ReflectedInfo
                {
                    Type = type,
                    HeadersProperty = headersProp,
                    NameProperty = nameProp,
                };
            }
        }
        catch (Exception)
        {
        }

        return null;
    }

    private static void EnsureEnabled()
    {
        if (!IsHttpFunctionalityEnabled)
        {
            throw new InvalidOperationException("Http functionality is not enabled, check IsHttpFunctionalityEnabled before calling this method");
        }
    }

    private class ReflectedInfo
    {
        public Type Type;
        public PropertyInfo NameProperty;
        public PropertyInfo HeadersProperty;
    }
}
