// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
    private static readonly ReflectedInfo? ReflectedValues = Initialize();

    public static bool IsHttpFunctionalityEnabled => ReflectedValues != null;

    public static string Name
    {
        get
        {
            AssertHttpEnabled();
            return ReflectedValues!.Name;
        }
    }

    public static object CreateNew()
    {
        AssertHttpEnabled();
        return Activator.CreateInstance(ReflectedValues!.Type)!;
    }

    /// <summary>
    /// Creates a new HttpRequestMessageProperty with the same method, query string, entity body
    /// suppression setting and headers as the supplied one. The returned object shares no state
    /// with the original, so it can be modified without affecting any other message that
    /// holds a reference to the original.
    /// </summary>
    /// <param name="httpRequestMessageProperty">The HttpRequestMessageProperty to copy.</param>
    /// <returns>The copy.</returns>
    public static object CreateCopy(object httpRequestMessageProperty)
    {
        AssertHttpEnabled();
        AssertIsFrameworkMessageProperty(httpRequestMessageProperty);

        var reflected = ReflectedValues!;
        var copy = CreateNew();

        reflected.MethodProperty.SetValue(copy, reflected.MethodProperty.GetValue(httpRequestMessageProperty));
        reflected.QueryStringProperty.SetValue(copy, reflected.QueryStringProperty.GetValue(httpRequestMessageProperty));
        reflected.SuppressEntityBodyProperty.SetValue(copy, reflected.SuppressEntityBodyProperty.GetValue(httpRequestMessageProperty));

        var source = reflected.HeadersFetcher.Fetch(httpRequestMessageProperty);
        var destination = reflected.HeadersFetcher.Fetch(copy);
        foreach (var key in source.AllKeys)
        {
            // Copy the raw value as stored. GetValues(string) re-parses the value on comma boundaries
            // and is not lossless for quoted values (for example a Digest Authorization header).
            if (key != null)
            {
                destination.Add(key, source[key]);
            }
        }

        return copy;
    }

    public static WebHeaderCollection GetHeaders(object httpRequestMessageProperty)
    {
        AssertHttpEnabled();
        AssertIsFrameworkMessageProperty(httpRequestMessageProperty);
        return ReflectedValues!.HeadersFetcher.Fetch(httpRequestMessageProperty);
    }

    private static ReflectedInfo? Initialize()
    {
        Type? type = null;
        try
        {
#pragma warning disable IDE0370 // Suppression is unnecessary
            type = Type.GetType(
                "System.ServiceModel.Channels.HttpRequestMessageProperty, System.ServiceModel, Version=0.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                true)!;
#pragma warning restore IDE0370 // Suppression is unnecessary

            var constructor = type.GetConstructor(Type.EmptyTypes)
                ?? throw new NotSupportedException("HttpRequestMessageProperty public parameterless constructor was not found");

            var headersProp = type.GetProperty("Headers", BindingFlags.Public | BindingFlags.Instance, null, typeof(WebHeaderCollection), [], null)
                ?? throw new NotSupportedException("HttpRequestMessageProperty.Headers property not found");

            var methodProp = GetReadWriteProperty(type, "Method", typeof(string));
            var queryStringProp = GetReadWriteProperty(type, "QueryString", typeof(string));
            var suppressEntityBodyProp = GetReadWriteProperty(type, "SuppressEntityBody", typeof(bool));

            var nameProp = type.GetProperty("Name", BindingFlags.Public | BindingFlags.Static, null, typeof(string), [], null)
                ?? throw new NotSupportedException("HttpRequestMessageProperty.Name property not found");

            return nameProp.GetValue(null) is not string name
                ? throw new NotSupportedException("HttpRequestMessageProperty.Name property was null")
                : new ReflectedInfo(
                    type: type,
                    name: name,
                    headersFetcher: new PropertyFetcher<WebHeaderCollection>("Headers"),
                    methodProperty: methodProp,
                    queryStringProperty: queryStringProp,
                    suppressEntityBodyProperty: suppressEntityBodyProp);
        }
        catch (Exception ex)
        {
            WcfInstrumentationEventSource.Log.HttpServiceModelReflectionFailedToBind(ex, type?.Assembly);
        }

        return null;
    }

    private static PropertyInfo GetReadWriteProperty(Type type, string name, Type propertyType)
    {
        var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance, null, propertyType, [], null);
        return property is { CanRead: true, CanWrite: true }
            ? property
            : throw new NotSupportedException($"HttpRequestMessageProperty.{name} read/write property not found");
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
        if (httpRequestMessageProperty == null || !httpRequestMessageProperty.GetType().Equals(ReflectedValues!.Type))
        {
            throw new ArgumentException("Object must be of type HttpRequestMessageProperty");
        }
    }

    private sealed class ReflectedInfo
    {
        public readonly Type Type;
        public readonly string Name;
        public readonly PropertyFetcher<WebHeaderCollection> HeadersFetcher;
        public readonly PropertyInfo MethodProperty;
        public readonly PropertyInfo QueryStringProperty;
        public readonly PropertyInfo SuppressEntityBodyProperty;

        public ReflectedInfo(
            Type type,
            string name,
            PropertyFetcher<WebHeaderCollection> headersFetcher,
            PropertyInfo methodProperty,
            PropertyInfo queryStringProperty,
            PropertyInfo suppressEntityBodyProperty)
        {
            this.Type = type;
            this.Name = name;
            this.HeadersFetcher = headersFetcher;
            this.MethodProperty = methodProperty;
            this.QueryStringProperty = queryStringProperty;
            this.SuppressEntityBodyProperty = suppressEntityBodyProperty;
        }
    }
}
