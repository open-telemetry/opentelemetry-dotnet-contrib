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
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Instrumentation.AWSLambda.Implementation
{
    /// <summary>
    /// Wrapper class for AWS Lambda handlers.
    /// </summary>
    public class AWSLambdaWrapper
    {
        private static readonly ActivitySource AWSLambdaActivitySource = new ActivitySource(AWSLambdaUtils.ActivitySourceName);

        /// <summary>
        /// Tracing wrapper for Lambda handler without Lambda context.
        /// </summary>
        /// <typeparam name="TInput">Input.</typeparam>
        /// <typeparam name="TResult">Output result.</typeparam>
        /// <param name="tracerProvider">TracerProvider passed in.</param>
        /// <param name="lambdaHandler">Lambda handler function passed in.</param>
        /// <param name="input">Instance of input.</param>
        /// <returns>Instance of output result.</returns>
        public static TResult Trace<TInput, TResult>(TracerProvider tracerProvider, Func<TInput, TResult> lambdaHandler, TInput input)
        {
            return Intercept(tracerProvider, () => lambdaHandler(input));
        }

        /// <summary>
        /// Tracing wrapper for Lambda handler without Lambda context.
        /// </summary>
        /// <typeparam name="TInput">Input.</typeparam>
        /// <param name="tracerProvider">TracerProvider passed in.</param>
        /// <param name="lambdaHandler">Lambda handler function passed in.</param>
        /// <param name="input">Instance of input.</param>
        public static void Trace<TInput>(TracerProvider tracerProvider, Action<TInput> lambdaHandler, TInput input)
        {
            Intercept(tracerProvider, () => lambdaHandler(input));
        }

        /// <summary>
        /// Tracing wrapper for async Lambda handler without Lambda context.
        /// </summary>
        /// <typeparam name="TInput">Input.</typeparam>
        /// <param name="tracerProvider">TracerProvider passed in.</param>
        /// <param name="lambdaHandler">Lambda handler function passed in.</param>
        /// <param name="input">Instance of input.</param>
        /// <returns>Task.</returns>
        public static async Task Trace<TInput>(TracerProvider tracerProvider, Func<TInput, Task> lambdaHandler, TInput input)
        {
            await Intercept(tracerProvider, () => lambdaHandler(input));
        }

        /// <summary>
        /// Tracing wrapper for async Lambda handler without Lambda context.
        /// </summary>
        /// <typeparam name="TInput">Input.</typeparam>
        /// <typeparam name="TResult">Output result.</typeparam>
        /// <param name="tracerProvider">TracerProvider passed in.</param>
        /// <param name="lambdaHandler">Lambda handler function passed in.</param>
        /// <param name="input">Instance of input.</param>
        /// <returns>Task of result.</returns>
        public static async Task<TResult> Trace<TInput, TResult>(TracerProvider tracerProvider, Func<TInput, Task<TResult>> lambdaHandler, TInput input)
        {
            return await Intercept(tracerProvider, () => lambdaHandler(input));
        }

        /// <summary>
        /// Tracing wrapper for Lambda handler.
        /// </summary>
        /// <typeparam name="TInput">Input.</typeparam>
        /// <typeparam name="TResult">Output result.</typeparam>
        /// <param name="tracerProvider">TracerProvider passed in.</param>
        /// <param name="lambdaHandler">Lambda handler function passed in.</param>
        /// <param name="input">Instance of input.</param>
        /// <param name="context">Instance of lambda context.</param>
        /// <returns>Instance of output result.</returns>
        public static TResult Trace<TInput, TResult>(TracerProvider tracerProvider, Func<TInput, ILambdaContext, TResult> lambdaHandler, TInput input, ILambdaContext context)
        {
            return Intercept(tracerProvider, () => lambdaHandler(input, context), context);
        }

        /// <summary>
        /// Tracing wrapper for Lambda handler.
        /// </summary>
        /// <typeparam name="TInput">Input.</typeparam>
        /// <param name="tracerProvider">TracerProvider passed in.</param>
        /// <param name="lambdaHandler">Lambda handler function passed in.</param>
        /// <param name="input">Instance of input.</param>
        /// <param name="context">Instance of lambda context.</param>
        public static void Trace<TInput>(TracerProvider tracerProvider, Action<TInput, ILambdaContext> lambdaHandler, TInput input, ILambdaContext context)
        {
            Intercept(tracerProvider, () => lambdaHandler(input, context), context);
        }

        /// <summary>
        /// Tracing wrapper for async Lambda handler.
        /// </summary>
        /// <typeparam name="TInput">Input.</typeparam>
        /// <param name="tracerProvider">TracerProvider passed in.</param>
        /// <param name="lambdaHandler">Lambda handler function passed in.</param>
        /// <param name="input">Instance of input.</param>
        /// <param name="context">Instance of lambda context.</param>
        /// <returns>Task.</returns>
        public static async Task Trace<TInput>(TracerProvider tracerProvider, Func<TInput, ILambdaContext, Task> lambdaHandler, TInput input, ILambdaContext context)
        {
            await Intercept(tracerProvider, () => lambdaHandler(input, context), context);
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
        /// <returns>Task of result.</returns>
        public static async Task<TResult> Trace<TInput, TResult>(TracerProvider tracerProvider, Func<TInput, ILambdaContext, Task<TResult>> lambdaHandler, TInput input, ILambdaContext context)
        {
            return await Intercept(tracerProvider, () => lambdaHandler(input, context), context);
        }

        private static TResult Intercept<TResult>(TracerProvider tracerProvider, Func<TResult> method, ILambdaContext context = null)
        {
            var lambdaActivity = OnFunctionStart(context);
            try
            {
                return method();
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

        private static void Intercept(TracerProvider tracerProvider, Action method, ILambdaContext context = null)
        {
            var lambdaActivity = OnFunctionStart(context);
            try
            {
                method();
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

        private static async Task<TResult> Intercept<TResult>(TracerProvider tracerProvider, Func<Task<TResult>> method, ILambdaContext context = null)
        {
            var lambdaActivity = OnFunctionStart(context);
            try
            {
                return await method();
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

        private static async Task Intercept(TracerProvider tracerProvider, Func<Task> method, ILambdaContext context = null)
        {
            var lambdaActivity = OnFunctionStart(context);
            try
            {
                await method();
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

        private static Activity OnFunctionStart(ILambdaContext context = null)
        {
            Activity activity = null;

            var parentContext = AWSLambdaUtils.GetParentContext();
            if (parentContext != default)
            {
                var activityName = AWSLambdaUtils.GetFunctionName(context);
                activity = AWSLambdaActivitySource.StartActivity(activityName, ActivityKind.Server, parentContext);

                if (activity != null && context != null)
                {
                    if (activity.IsAllDataRequested)
                    {
                        if (context.AwsRequestId != null)
                        {
                            activity.SetTag(AWSLambdaSemanticConventions.AttributeFaasExecution, context.AwsRequestId);
                        }

                        var functionArn = context.InvokedFunctionArn;
                        if (functionArn != null)
                        {
                            activity.SetTag(AWSLambdaSemanticConventions.AttributeFaasID, functionArn);

                            var accountId = AWSLambdaUtils.GetAccountId(functionArn);
                            if (accountId != null)
                            {
                                activity.SetTag(AWSLambdaSemanticConventions.AttributeCloudAccountID, accountId);
                            }
                        }
                    }
                }
            }

            return activity;
        }

        private static void OnFunctionStop(Activity activity, TracerProvider tracerProvider)
        {
            if (activity != null)
            {
                activity.Stop();
            }

            // force flush before function quit in case of Lambda freeze.
            tracerProvider.ForceFlush();
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
    }
}
