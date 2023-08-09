// <copyright file="AWSLambdaWrapper.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using OpenTelemetry.Instrumentation.AWSLambda.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AWSLambda;

/// <summary>
/// Wrapper class for AWS Lambda handlers.
/// </summary>
public static class AWSLambdaWrapper
{
    private static readonly AssemblyName AssemblyName = typeof(AWSLambdaWrapper).Assembly.GetName();

    [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1202:ElementsMustBeOrderedByAccess", Justification = "Initialization order.")]
    internal static readonly string ActivitySourceName = AssemblyName.Name;

    private static readonly Version Version = AssemblyName.Version;
    private static readonly ActivitySource AWSLambdaActivitySource = new(ActivitySourceName, Version.ToString());

    /// <summary>
    /// Gets or sets a value indicating whether AWS X-Ray propagation should be ignored. Default value is false.
    /// </summary>
    internal static bool DisableAwsXRayContextExtraction { get; set; }

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters

    /// <summary>
    /// Tracing wrapper for Lambda handler.
    /// </summary>
    /// <typeparam name="TInput">Input.</typeparam>
    /// <typeparam name="TResult">Output result.</typeparam>
    /// <param name="tracerProvider">TracerProvider passed in.</param>
    /// <param name="lambdaHandler">Lambda handler function passed in.</param>
    /// <param name="input">Instance of input.</param>
    /// <param name="context">Instance of lambda context.</param>
    /// <param name="parentContext">
    /// The optional parent context <see cref="ActivityContext"/> is used for Activity object creation.
    /// If no parent context provided, incoming request is used to extract one.
    /// If parent is not extracted from incoming request then X-Ray propagation is used to extract one
    /// unless X-Ray propagation is disabled in the configuration for this wrapper.
    /// </param>
    /// <returns>Instance of output result.</returns>
    public static TResult Trace<TInput, TResult>(
        TracerProvider? tracerProvider,
        Func<TInput, ILambdaContext, TResult> lambdaHandler,
        TInput input,
        ILambdaContext context,
        ActivityContext parentContext = default)
    {
        Guard.ThrowIfNull(lambdaHandler);

        return TraceInternal(tracerProvider, lambdaHandler, input, context, parentContext);
    }

    /// <summary>
    /// Tracing wrapper for Lambda handler.
    /// </summary>
    /// <typeparam name="TInput">Input.</typeparam>
    /// <param name="tracerProvider">TracerProvider passed in.</param>
    /// <param name="lambdaHandler">Lambda handler function passed in.</param>
    /// <param name="input">Instance of input.</param>
    /// <param name="context">Instance of lambda context.</param>
    /// <param name="parentContext">
    /// The optional parent context <see cref="ActivityContext"/> is used for Activity object creation.
    /// If no parent context provided, incoming request is used to extract one.
    /// If parent is not extracted from incoming request then X-Ray propagation is used to extract one
    /// unless X-Ray propagation is disabled in the configuration for this wrapper.
    /// </param>
    public static void Trace<TInput>(
        TracerProvider? tracerProvider,
        Action<TInput, ILambdaContext> lambdaHandler,
        TInput input,
        ILambdaContext context,
        ActivityContext parentContext = default)
    {
        Guard.ThrowIfNull(lambdaHandler);

        object? Handler(TInput input, ILambdaContext context)
        {
            lambdaHandler(input, context);
            return null;
        }

        TraceInternal(tracerProvider, Handler, input, context, parentContext);
    }

    /// <summary>
    /// Tracing wrapper for async Lambda handler.
    /// </summary>
    /// <typeparam name="TInput">Input.</typeparam>
    /// <param name="tracerProvider">TracerProvider passed in.</param>
    /// <param name="lambdaHandler">Lambda handler function passed in.</param>
    /// <param name="input">Instance of input.</param>
    /// <param name="context">Lambda context (optional, but strongly recommended).</param>
    /// <param name="parentContext">
    /// The optional parent context <see cref="ActivityContext"/> is used for Activity object creation.
    /// If no parent context provided, incoming request is used to extract one.
    /// If parent is not extracted from incoming request then X-Ray propagation is used to extract one
    /// unless X-Ray propagation is disabled in the configuration for this wrapper.
    /// </param>
    /// <returns>Task.</returns>
    public static Task TraceAsync<TInput>(
        TracerProvider? tracerProvider,
        Func<TInput, ILambdaContext, Task> lambdaHandler,
        TInput input,
        ILambdaContext context,
        ActivityContext parentContext = default)
    {
        Guard.ThrowIfNull(lambdaHandler);

        async Task<object?> Handler(TInput input, ILambdaContext context)
        {
            await lambdaHandler(input, context).ConfigureAwait(false);
            return null;
        }

        return TraceInternalAsync(tracerProvider, Handler, input, context, parentContext);
    }

    /// <summary>
    /// Tracing wrapper for async Lambda handler.
    /// </summary>
    /// <typeparam name="TInput">Input.</typeparam>
    /// <typeparam name="TResult">Output result.</typeparam>
    /// <param name="tracerProvider">TracerProvider passed in.</param>
    /// <param name="lambdaHandler">Lambda handler function passed in.</param>
    /// <param name="input">Instance of input.</param>
    /// <param name="context">Instance of lambda context.</param>
    /// <param name="parentContext">
    /// The optional parent context <see cref="ActivityContext"/> is used for Activity object creation.
    /// If no parent context provided, incoming request is used to extract one.
    /// If parent is not extracted from incoming request then X-Ray propagation is used to extract one
    /// unless X-Ray propagation is disabled in the configuration for this wrapper.
    /// </param>
    /// <returns>Task of result.</returns>
    public static Task<TResult> TraceAsync<TInput, TResult>(
        TracerProvider? tracerProvider,
        Func<TInput, ILambdaContext, Task<TResult>> lambdaHandler,
        TInput input,
        ILambdaContext context,
        ActivityContext parentContext = default)
    {
        Guard.ThrowIfNull(lambdaHandler);

        return TraceInternalAsync(tracerProvider, lambdaHandler, input, context, parentContext);
    }

#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters

    internal static Activity? OnFunctionStart<TInput>(TInput input, ILambdaContext context, ActivityContext parentContext = default)
    {
        IEnumerable<ActivityLink>? links = null;
        if (parentContext == default)
        {
            (parentContext, links) = AWSLambdaUtils.ExtractParentContext(input);
            if (parentContext == default && !DisableAwsXRayContextExtraction)
            {
                parentContext = AWSLambdaUtils.GetXRayParentContext();
            }
        }

        var functionTags = AWSLambdaUtils.GetFunctionTags(input, context);
        var httpTags = AWSLambdaHttpUtils.GetHttpTags(input);

        // We assume that functionTags and httpTags have no intersection.
        var activityName = AWSLambdaUtils.GetFunctionName(context) ?? "AWS Lambda Invoke";
        var activity = AWSLambdaActivitySource.StartActivity(activityName, ActivityKind.Server, parentContext, functionTags.Concat(httpTags)!, links);

        return activity;
    }

    private static void OnFunctionStop(Activity? activity, TracerProvider? tracerProvider)
    {
        activity?.Stop();

        // force flush before function quit in case of Lambda freeze.
        tracerProvider?.ForceFlush();
    }

    private static void OnException(Activity? activity, Exception exception)
    {
        if (activity != null)
        {
            if (activity.IsAllDataRequested)
            {
                activity.RecordException(exception);
                activity.SetStatus(Status.Error.WithDescription(exception.Message));
            }
        }
    }

    private static TResult TraceInternal<TInput, TResult>(
        TracerProvider? tracerProvider,
        Func<TInput, ILambdaContext, TResult> handler,
        TInput input,
        ILambdaContext context,
        ActivityContext parentContext = default)
    {
        Guard.ThrowIfNull(context);

        var activity = OnFunctionStart(input, context, parentContext);
        try
        {
            var result = handler(input, context);
            AWSLambdaHttpUtils.SetHttpTagsFromResult(activity, result);
            return result;
        }
        catch (Exception ex)
        {
            OnException(activity, ex);

            throw;
        }
        finally
        {
            OnFunctionStop(activity, tracerProvider);
        }
    }

    private static async Task<TResult> TraceInternalAsync<TInput, TResult>(
        TracerProvider? tracerProvider,
        Func<TInput, ILambdaContext, Task<TResult>> handlerAsync,
        TInput input,
        ILambdaContext context,
        ActivityContext parentContext = default)
    {
        Guard.ThrowIfNull(context);

        var activity = OnFunctionStart(input, context, parentContext);
        try
        {
            var result = await handlerAsync(input, context).ConfigureAwait(false);
            AWSLambdaHttpUtils.SetHttpTagsFromResult(activity, result);
            return result;
        }
        catch (Exception ex)
        {
            OnException(activity, ex);

            throw;
        }
        finally
        {
            OnFunctionStop(activity, tracerProvider);
        }
    }
}
