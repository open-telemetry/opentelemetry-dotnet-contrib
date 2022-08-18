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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using OpenTelemetry.Instrumentation.AWSLambda.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AWSLambda
{
    /// <summary>
    /// Wrapper class for AWS Lambda handlers.
    /// </summary>
    public class AWSLambdaWrapper
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
            TracerProvider tracerProvider,
            Func<TInput, ILambdaContext, TResult> lambdaHandler,
            TInput input,
            ILambdaContext context,
            ActivityContext parentContext = default)
        {
            TResult result = default;
            Action action = () => result = lambdaHandler(input, context);
            TraceInternal(tracerProvider, action, input, context, parentContext);
            return result;
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
            TracerProvider tracerProvider,
            Action<TInput, ILambdaContext> lambdaHandler,
            TInput input,
            ILambdaContext context,
            ActivityContext parentContext = default)
        {
            Action action = () => lambdaHandler(input, context);
            TraceInternal(tracerProvider, action, input, context, parentContext);
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
        public static Task Trace<TInput>(
            TracerProvider tracerProvider,
            Func<TInput, ILambdaContext, Task> lambdaHandler,
            TInput input,
            ILambdaContext context,
            ActivityContext parentContext = default)
        {
            Func<Task> action = async () => await lambdaHandler(input, context);
            return TraceInternalAsync(tracerProvider, action, input, context, parentContext);
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
        public static async Task<TResult> Trace<TInput, TResult>(
            TracerProvider tracerProvider,
            Func<TInput, ILambdaContext, Task<TResult>> lambdaHandler,
            TInput input,
            ILambdaContext context,
            ActivityContext parentContext = default)
        {
            TResult result = default;
            Func<Task> action = async () => result = await lambdaHandler(input, context);
            await TraceInternalAsync(tracerProvider, action, input, context, parentContext);
            return result;
        }

        /// <summary>
        /// Tracing wrapper for Lambda handler.
        /// </summary>
        /// <param name="tracerProvider">TracerProvider passed in.</param>
        /// <param name="lambdaHandler">Lambda handler function passed in.</param>
        /// <param name="context">Instance of lambda context.</param>
        /// <param name="parentContext">
        /// The optional parent context <see cref="ActivityContext"/> is used for Activity object creation.
        /// If no parent context provided, incoming request is used to extract one.
        /// If parent is not extracted from incoming request then X-Ray propagation is used to extract one
        /// unless X-Ray propagation is disabled in the configuration for this wrapper.
        /// </param>
        public static void Trace(
            TracerProvider tracerProvider,
            Action<ILambdaContext> lambdaHandler,
            ILambdaContext context,
            ActivityContext parentContext = default)
        {
            Action action = () => lambdaHandler(context);
            TraceInternal<object>(tracerProvider, action, null, context, parentContext);
        }

        /// <summary>
        /// Tracing wrapper for async Lambda handler.
        /// </summary>
        /// <param name="tracerProvider">TracerProvider passed in.</param>
        /// <param name="lambdaHandler">Lambda handler function passed in.</param>
        /// <param name="context">Instance of lambda context.</param>
        /// <param name="parentContext">
        /// The optional parent context <see cref="ActivityContext"/> is used for Activity object creation.
        /// If no parent context provided, incoming request is used to extract one.
        /// If parent is not extracted from incoming request then X-Ray propagation is used to extract one
        /// unless X-Ray propagation is disabled in the configuration.
        /// </param>
        /// <returns>Task.</returns>
        public static Task Trace(
            TracerProvider tracerProvider,
            Func<ILambdaContext, Task> lambdaHandler,
            ILambdaContext context,
            ActivityContext parentContext = default)
        {
            Func<Task> action = async () => await lambdaHandler(context);
            return TraceInternalAsync<object>(tracerProvider, action, null, context, parentContext);
        }

        internal static Activity OnFunctionStart<TInput>(TInput input, ILambdaContext context, ActivityContext parentContext = default)
        {
            if (parentContext == default)
            {
                parentContext = AWSLambdaUtils.ExtractParentContext(input);
                if (parentContext == default && !DisableAwsXRayContextExtraction)
                {
                    parentContext = AWSLambdaUtils.GetXRayParentContext();
                }
            }

            var tags = AWSLambdaUtils.GetFunctionTags(input, context);
            var activityName = AWSLambdaUtils.GetFunctionName(context) ?? "AWS Lambda Invoke";
            var activity = AWSLambdaActivitySource.StartActivity(activityName, ActivityKind.Server, parentContext, tags);

            return activity;
        }

        private static void OnFunctionStop(Activity activity, TracerProvider tracerProvider)
        {
            if (activity != null)
            {
                activity.Stop();
            }

            // force flush before function quit in case of Lambda freeze.
            tracerProvider?.ForceFlush();
        }

        private static void OnException(Activity activity, Exception exception)
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

        private static void TraceInternal<TInput>(
            TracerProvider tracerProvider,
            Action handler,
            TInput input,
            ILambdaContext context,
            ActivityContext parentContext = default)
        {
            var lambdaActivity = OnFunctionStart(input, context, parentContext);
            try
            {
                handler();
            }
            catch (Exception ex)
            {
                OnException(lambdaActivity, ex);

                throw;
            }
            finally
            {
                OnFunctionStop(lambdaActivity, tracerProvider);
            }
        }

        private static async Task TraceInternalAsync<TInput>(
            TracerProvider tracerProvider,
            Func<Task> handlerAsync,
            TInput input,
            ILambdaContext context,
            ActivityContext parentContext = default)
        {
            var lambdaActivity = OnFunctionStart(input, context, parentContext);
            try
            {
                await handlerAsync();
            }
            catch (Exception ex)
            {
                OnException(lambdaActivity, ex);

                throw;
            }
            finally
            {
                OnFunctionStop(lambdaActivity, tracerProvider);
            }
        }
    }
}
