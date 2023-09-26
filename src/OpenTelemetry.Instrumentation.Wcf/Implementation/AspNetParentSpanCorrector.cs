// <copyright file="AspNetParentSpanCorrector.cs" company="OpenTelemetry Authors">
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

#if NETFRAMEWORK
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

/// <summary>
/// When WCF is hosted in ASP.NET and the ASP.NET telemetry instrumentation is installed, the execution context does not flow from ASP.NET into WCF
/// so the span which is created by ASP.NET is not visible to WCF. When OpenTelemetry.Instrumentation.Wcf then creates its own span it uses the parent
/// which is passed from the caller. This results in the ASP.NET span and the WCF span being siblings off the same parent when, really, the WCF span
/// should actually be a child of the ASP.NET (transport) span. This class corrects that behavior so the generated spans are correctly parented.
///
/// The way it does that is it hooks into the ASP.NET telemetry and rewrites the incoming request headers to reflect the ASP.NET parent (so, as far
/// as WCF is concerned, it appears like the ASP.NET parent span was sent from the caller). It does this all via reflection to avoid having explicit
/// dependencies on System.Web and OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule. This way this behavior is only enabled if the consumer
/// also has the ASP.NET instrumentation installed.
/// </summary>
internal static class AspNetParentSpanCorrector
{
    private const string TelemetryHttpModuleTypeName = "OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule";
    private const string TelemetryHttpModuleOptionsTypeName = "OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModuleOptions, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule";

    private static readonly ReflectedInfo ReflectedValues = Initialize();
    private static readonly PropertyFetcher<object> RequestFetcher = new PropertyFetcher<object>("Request");
    private static readonly PropertyFetcher<NameValueCollection> HeadersFetcher = new PropertyFetcher<NameValueCollection>("Headers");
    private static bool isRegistered;

    public static void Register()
    {
        if (!isRegistered && ReflectedValues != null)
        {
            ReflectedValues.SubscribeToOnRequestStartedCallback();
            isRegistered = true;
        }
    }

    private static void OnRequestStarted(Activity activity, object context)
    {
        var request = RequestFetcher.Fetch(context);
        var headers = HeadersFetcher.Fetch(request);

        ReflectedValues.SetHeadersReadOnly(headers, false);
        try
        {
            ReflectedValues.GetTelemetryHttpModulePropagator().Inject(
                new PropagationContext(activity.Context, Baggage.Current),
                headers,
                (headers, name, value) => headers[name] = value);
        }
        finally
        {
            ReflectedValues.SetHeadersReadOnly(headers, true);
        }
    }

    private static ReflectedInfo Initialize()
    {
        try
        {
            var isReadOnlyProp = typeof(NameValueCollection).GetProperty("IsReadOnly", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            if (isReadOnlyProp == null)
            {
                throw new NotSupportedException("NameValueCollection.IsReadOnly property not found");
            }

            var setHeadersReadOnly = (Action<NameValueCollection, bool>)isReadOnlyProp.SetMethod.CreateDelegate(typeof(Action<NameValueCollection, bool>));

            return new ReflectedInfo
            {
                SetHeadersReadOnly = setHeadersReadOnly,
                GetTelemetryHttpModulePropagator = GenerateGetPropagatorLambda(),
                SubscribeToOnRequestStartedCallback = GenerateSubscribeLambda(),
            };
        }
        catch (Exception ex)
        {
            WcfInstrumentationEventSource.Log.AspNetReflectionFailedToBind(ex);
        }

        return null;
    }

    private static Func<TextMapPropagator> GenerateGetPropagatorLambda()
    {
        // this method generates this lambda:
        // () => TelemetryHttpModule.Options.TextMapPropagator

        var telemetryHttpModuleType = Type.GetType(TelemetryHttpModuleTypeName, true);
        var options = Expression.Property(null, telemetryHttpModuleType, "Options");
        var propertyReferenceExpression = Expression.Property(options, "TextMapPropagator");
        return (Func<TextMapPropagator>)Expression.Lambda(propertyReferenceExpression).Compile();
    }

    private static Func<object> GenerateSubscribeLambda()
    {
        // this method effectively generates this lambda:
        // () => TelemetryHttpModule.Options.OnRequestStartedCallback += OnRequestStarted
        // technically it generates this:
        // () => TelemetryHttpModule.Options.OnRequestStartedCallback =
        //   (Action<Activity, HttpContext>)addOurCallbackToDelegate(TelemetryHttpModule.Options.OnRequestStartedCallback);

        var telemetryHttpModuleType = Type.GetType(TelemetryHttpModuleTypeName, true);
        var telemetryHttpModuleOptionsType = Type.GetType(TelemetryHttpModuleOptionsTypeName, true);
        var onRequestStartedProp = telemetryHttpModuleOptionsType.GetProperty("OnRequestStartedCallback");
        if (onRequestStartedProp == null)
        {
            throw new NotSupportedException("TelemetryHttpModuleOptions.OnRequestStartedCallback property not found");
        }

        Func<Delegate, Delegate> addOurCallbackToDelegate = (existingCallback) =>
        {
            var myCallback = OnRequestStarted;
            var myCallbackProperlyTyped = Delegate.CreateDelegate(onRequestStartedProp.PropertyType, myCallback.Target, myCallback.Method);
            return Delegate.Combine(existingCallback, myCallbackProperlyTyped);
        };

        var options = Expression.Property(null, telemetryHttpModuleType, "Options");
        var callbackProperty = Expression.Property(options, onRequestStartedProp);
        var combinedDelegate = Expression.Call(Expression.Constant(addOurCallbackToDelegate.Target), addOurCallbackToDelegate.Method, callbackProperty);
        var subscribeExpression = Expression.Assign(callbackProperty, Expression.Convert(combinedDelegate, onRequestStartedProp.PropertyType));
        return (Func<object>)Expression.Lambda(subscribeExpression).Compile();
    }

    private sealed class ReflectedInfo
    {
        public Action<NameValueCollection, bool> SetHeadersReadOnly;
        public Func<TextMapPropagator> GetTelemetryHttpModulePropagator;
        public Func<object> SubscribeToOnRequestStartedCallback;
    }
}
#endif
