// <copyright file="GrpcCoreServerInterceptorTests.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Contrib.Instrumentation.GrpcCore.Test
{
    /// <summary>
    /// Grpc Core server interceptor tests.
    /// </summary>
    [TestClass]
    public class GrpcCoreServerInterceptorTests
    {
        /// <summary>
        /// Validates a successful UnaryServerHandler call.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task UnaryServerHandlerSuccess()
        {
            await this.TestHandlerSuccess(FoobarService.MakeUnaryAsyncRequest).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates a failed UnaryServerHandler call.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task UnaryServerHandlerFail()
        {
            await this.TestHandlerFailure(FoobarService.MakeUnaryAsyncRequest).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates a successful ClientStreamingServerHandler call.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task ClientStreamingServerHandlerSuccess()
        {
            await this.TestHandlerSuccess(FoobarService.MakeClientStreamingRequest).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates a failed ClientStreamingServerHandler call.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task ClientStreamingServerHandlerFail()
        {
            await this.TestHandlerFailure(FoobarService.MakeClientStreamingRequest).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates a successful ServerStreamingServerHandler call.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task ServerStreamingServerHandlerSuccess()
        {
            await this.TestHandlerSuccess(FoobarService.MakeServerStreamingRequest).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates a failed ServerStreamingServerHandler call.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task ServerStreamingServerHandlerFail()
        {
            await this.TestHandlerFailure(FoobarService.MakeServerStreamingRequest).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates a successful DuplexStreamingServerHandler call.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task DuplexStreamingServerHandlerSuccess()
        {
            await this.TestHandlerSuccess(FoobarService.MakeDuplexStreamingRequest).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates a failed DuplexStreamingServerHandler call.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task DuplexStreamingServerHandlerFail()
        {
            await this.TestHandlerFailure(FoobarService.MakeDuplexStreamingRequest).ConfigureAwait(false);
        }

        /// <summary>
        /// A common method to test server interceptor handler success.
        /// </summary>
        /// <param name="clientRequestFunc">The specific client request function.</param>
        /// <returns>A Task.</returns>
        private async Task TestHandlerSuccess(Func<Foobar.FoobarClient, Task> clientRequestFunc)
        {
            // starts the server with the server interceptor
            var interceptorOptions = new ServerTracingInterceptorOptions { Propagator = new TraceContextPropagator(), RecordMessageEvents = true };
            using var server = FoobarService.Start(new ServerTracingInterceptor(interceptorOptions));

            // No parent Activity, no context from header
            using (var activityListener = new InterceptorActivityListener())
            {
                var client = FoobarService.ConstructRpcClient(server.UriString);
                await clientRequestFunc(client).ConfigureAwait(false);

                var activity = activityListener.Activity;
                GrpcCoreClientInterceptorTests.ValidateCommonActivityTags(activity, StatusCode.OK, interceptorOptions.RecordMessageEvents);
                Assert.AreEqual(default, activity.ParentSpanId);
            }

            // No parent Activity, context from header
            using (var activityListener = new InterceptorActivityListener())
            {
                var client = FoobarService.ConstructRpcClient(
                    server.UriString,
                    additionalMetadata: new List<Metadata.Entry>
                    {
                        new Metadata.Entry("traceparent", FoobarService.DefaultTraceparentWithSampling),
                    });

                await clientRequestFunc(client).ConfigureAwait(false);

                var activity = activityListener.Activity;
                GrpcCoreClientInterceptorTests.ValidateCommonActivityTags(activity, StatusCode.OK, interceptorOptions.RecordMessageEvents);
                Assert.AreEqual(FoobarService.DefaultParentFromTraceparentHeader.SpanId, activity.ParentSpanId);
            }
        }

        /// <summary>
        /// A common method to test server interceptor handler failure.
        /// </summary>
        /// <param name="clientRequestFunc">The specific client request function.</param>
        /// <returns>A Task.</returns>
        private async Task TestHandlerFailure(Func<Foobar.FoobarClient, Task> clientRequestFunc)
        {
            // starts the server with the server interceptor
            var interceptorOptions = new ServerTracingInterceptorOptions { Propagator = new TraceContextPropagator() };
            using var server = FoobarService.Start(new ServerTracingInterceptor(interceptorOptions));

            using var activityListener = new InterceptorActivityListener();
            var client = FoobarService.ConstructRpcClient(
                server.UriString,
                additionalMetadata: new List<Metadata.Entry>
                {
                    new Metadata.Entry("traceparent", FoobarService.DefaultTraceparentWithSampling),
                    new Metadata.Entry(FoobarService.RequestHeaderFailWithStatusCode, StatusCode.ResourceExhausted.ToString()),
                    new Metadata.Entry(FoobarService.RequestHeaderErrorDescription, "fubar"),
                });

            try
            {
                await clientRequestFunc(client).ConfigureAwait(false);
                Assert.Fail();
            }
            catch (RpcException)
            {
            }

            var activity = activityListener.Activity;
            GrpcCoreClientInterceptorTests.ValidateCommonActivityTags(activity, StatusCode.ResourceExhausted, interceptorOptions.RecordMessageEvents);
            Assert.AreEqual(FoobarService.DefaultParentFromTraceparentHeader.SpanId, activity.ParentSpanId);
        }
    }
}
