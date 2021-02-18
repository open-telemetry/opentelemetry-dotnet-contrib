// <copyright file="GrpcCoreClientInterceptorTests.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Contrib.Instrumentation.GrpcCore.Test
{
    /// <summary>
    /// Grpc Core client interceptor tests.
    /// </summary>
    [TestClass]
    public class GrpcCoreClientInterceptorTests
    {
        /// <summary>
        /// The server.
        /// </summary>
        private static FoobarService.DisposableServer server;

        /// <summary>
        /// The Foobar service uri.
        /// </summary>
        private static string foobarUri;

        /// <summary>
        /// Class initialize.
        /// </summary>
        /// <param name="testContext">The test context.</param>
        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            server = FoobarService.Start();
            foobarUri = server.UriString;
        }

        /// <summary>
        /// Class cleanup.
        /// </summary>
        [ClassCleanup]
        public static void ClassCleanup()
        {
            server.Dispose();
        }

        /// <summary>
        /// Validates a successful AsyncUnary call.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task AsyncUnarySuccess()
        {
            await this.TestHandlerSuccess(FoobarService.MakeUnaryAsyncRequest).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates a failed AsyncUnary call because the endpoint isn't there.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task AsyncUnaryUnavailable()
        {
            await this.TestHandlerFailure(
                FoobarService.MakeUnaryAsyncRequest,
                StatusCode.Unavailable,
                validateErrorDescription: false,
                "dns:i.dont.exist:77923").ConfigureAwait(false);
        }

        /// <summary>
        /// Validates a failed AsyncUnary call because the service returned an error.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task AsyncUnaryFail()
        {
            await this.TestHandlerFailure(FoobarService.MakeUnaryAsyncRequest).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates a failed AsyncUnary call because the client is disposed before completing the RPC.
        /// </summary>
        [TestMethod]
        public void AsyncUnaryDisposed()
        {
            static void MakeRequest(Foobar.FoobarClient client)
            {
                using var call = client.UnaryAsync(FoobarService.DefaultRequestMessage);
            }

            this.TestActivityIsCancelledWhenHandlerDisposed(MakeRequest);
        }

        /// <summary>
        /// Validates a successful ClientStreaming call.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task ClientStreamingSuccess()
        {
            await this.TestHandlerSuccess(FoobarService.MakeClientStreamingRequest).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates a failed ClientStreaming call.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task ClientStreamingFail()
        {
            await this.TestHandlerFailure(FoobarService.MakeClientStreamingRequest).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates a failed ClientStreaming call because the client is disposed before completing the RPC.
        /// </summary>
        [TestMethod]
        public void ClientStreamingDisposed()
        {
            static void MakeRequest(Foobar.FoobarClient client)
            {
                using var call = client.ClientStreaming();
            }

            this.TestActivityIsCancelledWhenHandlerDisposed(MakeRequest);
        }

        /// <summary>
        /// Validates a successful ServerStreaming call.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task ServerStreamingSuccess()
        {
            await this.TestHandlerSuccess(FoobarService.MakeServerStreamingRequest).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates a failed ServerStreaming call.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task ServerStreamingFail()
        {
            await this.TestHandlerFailure(FoobarService.MakeServerStreamingRequest).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates a failed ServerStreaming call because the client is disposed before completing the RPC.
        /// </summary>
        [TestMethod]
        public void ServerStreamingDisposed()
        {
            static void MakeRequest(Foobar.FoobarClient client)
            {
                using var call = client.ServerStreaming(FoobarService.DefaultRequestMessage);
            }

            this.TestActivityIsCancelledWhenHandlerDisposed(MakeRequest);
        }

        /// <summary>
        /// Validates a successful DuplexStreaming call.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task DuplexStreamingSuccess()
        {
            await this.TestHandlerSuccess(FoobarService.MakeDuplexStreamingRequest).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates a failed DuplexStreaming call.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task DuplexStreamingFail()
        {
            await this.TestHandlerFailure(FoobarService.MakeDuplexStreamingRequest).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates a failed DuplexStreaming call because the client is disposed before completing the RPC.
        /// </summary>
        [TestMethod]
        public void DuplexStreamingDisposed()
        {
            static void MakeRequest(Foobar.FoobarClient client)
            {
                using var call = client.DuplexStreaming();
            }

            this.TestActivityIsCancelledWhenHandlerDisposed(MakeRequest);
        }

        /// <summary>
        /// Validates that a downstream interceptor has access to the created Activity
        /// and that the caller always sees the correct activity, not our created Activity.
        /// </summary>
        /// <returns>A Task.</returns>
        [TestMethod]
        public async Task DownstreamInterceptorActivityAccess()
        {
            var channel = new Channel(foobarUri, ChannelCredentials.Insecure);
            var callInvoker = channel.CreateCallInvoker();

            // Activity has a parent
            using var parentActivity = new Activity("foo").Start();

            // Order of interceptor invocation will be ClientTracingInterceptor -> MetadataInjector
            callInvoker = callInvoker.Intercept(
                metadata =>
                {
                    // This Func is called as part of an internal MetadataInjector interceptor created by gRPC Core.
                    Assert.IsTrue(Activity.Current.Source == GrpcCoreInstrumentation.ActivitySource);
                    Assert.AreEqual(parentActivity, Activity.Current.Parent);

                    // Set a tag on the Activity and make sure we can see it afterwardsd
                    Activity.Current.SetTag("foo", "bar");
                    return metadata;
                });

            var interceptorOptions = new ClientTracingInterceptorOptions();
            callInvoker = callInvoker.Intercept(new ClientTracingInterceptor(new ClientTracingInterceptorOptions()));
            var client = new Foobar.FoobarClient(callInvoker);

            static void ValidateNewTagOnActivity(InterceptorActivityListener listener)
            {
                var createdActivity = listener.Activity;
                Assert.IsTrue(createdActivity.TagObjects.Any(t => t.Key == "foo" && (string)t.Value == "bar"));
            }

            // Check the blocking async call
            using (var activityListener = new InterceptorActivityListener())
            {
                Assert.AreEqual(parentActivity, Activity.Current);
                var response = client.Unary(FoobarService.DefaultRequestMessage);

                Assert.AreEqual(parentActivity, Activity.Current);

                ValidateNewTagOnActivity(activityListener);
            }

            // Check unary async
            using (var activityListener = new InterceptorActivityListener())
            {
                Assert.AreEqual(parentActivity, Activity.Current);
                using var call = client.UnaryAsync(FoobarService.DefaultRequestMessage);

                Assert.AreEqual(parentActivity, Activity.Current);

                _ = await call.ResponseAsync.ConfigureAwait(false);

                Assert.AreEqual(parentActivity, Activity.Current);

                ValidateNewTagOnActivity(activityListener);
            }

            // Check a streaming async call
            using (var activityListener = new InterceptorActivityListener())
            {
                Assert.AreEqual(parentActivity, Activity.Current);
                using var call = client.DuplexStreaming();

                Assert.AreEqual(parentActivity, Activity.Current);

                await call.RequestStream.WriteAsync(FoobarService.DefaultRequestMessage).ConfigureAwait(false);

                Assert.AreEqual(parentActivity, Activity.Current);

                await call.RequestStream.CompleteAsync().ConfigureAwait(false);

                Assert.AreEqual(parentActivity, Activity.Current);

                while (await call.ResponseStream.MoveNext().ConfigureAwait(false))
                {
                    Assert.AreEqual(parentActivity, Activity.Current);
                }

                Assert.AreEqual(parentActivity, Activity.Current);

                ValidateNewTagOnActivity(activityListener);
            }
        }

        /// <summary>
        /// Validates the common activity tags.
        /// </summary>
        /// <param name="activity">The activity.</param>
        /// <param name="expectedStatusCode">The expected status code.</param>
        /// <param name="recordedMessages">if set to <c>true</c> [recorded messages].</param>
        internal static void ValidateCommonActivityTags(
            Activity activity,
            Grpc.Core.StatusCode expectedStatusCode = Grpc.Core.StatusCode.OK,
            bool recordedMessages = false)
        {
            Assert.IsNotNull(activity);
            Assert.IsNotNull(activity.Tags);

            // The activity was stopped
            Assert.IsTrue(activity.Duration != default);

            // TagObjects contain non string values
            // Tags contains only string values
            Assert.IsTrue(activity.TagObjects.Any(t => t.Key == SemanticConventions.AttributeRpcSystem && (string)t.Value == "grpc"));
            Assert.IsTrue(activity.TagObjects.Any(t => t.Key == SemanticConventions.AttributeRpcService && (string)t.Value == "OpenTelemetry.Contrib.Instrumentation.GrpcCore.Test.Foobar"));
            Assert.IsTrue(activity.TagObjects.Any(t => t.Key == SemanticConventions.AttributeRpcMethod));
            Assert.IsTrue(activity.TagObjects.Any(t => t.Key == SemanticConventions.AttributeRpcGrpcStatusCode && (int)t.Value == (int)expectedStatusCode));

            // Cancelled is not an error.
            if (expectedStatusCode != StatusCode.OK && expectedStatusCode != StatusCode.Cancelled)
            {
                Assert.IsTrue(activity.TagObjects.Any(
                    t => t.Key == SemanticConventions.AttributeOtelStatusCode && (string)t.Value == "ERROR"));
            }

            if (recordedMessages)
            {
                // all methods accept a request and return a single response
                Assert.IsNotNull(activity.Events);
                var requestMessage = activity.Events.FirstOrDefault(ae => ae.Name == FoobarService.DefaultRequestMessage.GetType().Name);
                var responseMessage = activity.Events.FirstOrDefault(ae => ae.Name == FoobarService.DefaultResponseMessage.GetType().Name);

                static void ValidateCommonEventAttributes(ActivityEvent activityEvent)
                {
                    Assert.IsNotNull(activityEvent.Tags);
                    Assert.IsTrue(activityEvent.Tags.Any(t => t.Key == "name" && (string)t.Value == "message"));
                    Assert.IsTrue(activityEvent.Tags.Any(t => t.Key == SemanticConventions.AttributeMessageID && (int)t.Value == 1));
                }

                Assert.IsNotNull(requestMessage);
                Assert.IsNotNull(responseMessage);

                ValidateCommonEventAttributes(requestMessage);
                Assert.IsTrue(requestMessage.Tags.Any(t => t.Key == SemanticConventions.AttributeMessageType && (string)t.Value == "SENT"));
                Assert.IsTrue(requestMessage.Tags.Any(t => t.Key == SemanticConventions.AttributeMessageCompressedSize && (int)t.Value == FoobarService.DefaultRequestMessageSize));

                ValidateCommonEventAttributes(responseMessage);
                Assert.IsTrue(responseMessage.Tags.Any(t => t.Key == SemanticConventions.AttributeMessageType && (string)t.Value == "RECEIVED"));
                Assert.IsTrue(requestMessage.Tags.Any(t => t.Key == SemanticConventions.AttributeMessageCompressedSize && (int)t.Value == FoobarService.DefaultResponseMessageSize));
            }
        }

        /// <summary>
        /// Tests basic handler success.
        /// Also validates additional Activity tags written via the OnRpcStarted callback.
        /// </summary>
        /// <param name="clientRequestFunc">The client request function.</param>
        /// <returns>A Task.</returns>
        private async Task TestHandlerSuccess(Func<Foobar.FoobarClient, Task> clientRequestFunc)
        {
            var interceptorOptions = new ClientTracingInterceptorOptions
            {
                Propagator = new TraceContextPropagator(),
                RecordMessageEvents = true,
            };

            // No Activity parent
            using (var activityListener = new InterceptorActivityListener())
            {
                var client = FoobarService.ConstructRpcClient(foobarUri, new ClientTracingInterceptor(interceptorOptions));
                await clientRequestFunc(client).ConfigureAwait(false);

                Assert.AreEqual(default, Activity.Current);

                var activity = activityListener.Activity;
                ValidateCommonActivityTags(activity, StatusCode.OK, interceptorOptions.RecordMessageEvents);
                Assert.AreEqual(default, activity.ParentSpanId);
            }

            // Activity has a parent
            using (var activityListener = new InterceptorActivityListener())
            {
                using var parentActivity = new Activity("foo").Start();
                var client = FoobarService.ConstructRpcClient(foobarUri, new ClientTracingInterceptor(interceptorOptions));
                await clientRequestFunc(client).ConfigureAwait(false);

                Assert.AreEqual(parentActivity, Activity.Current);

                var activity = activityListener.Activity;
                ValidateCommonActivityTags(activity, StatusCode.OK, interceptorOptions.RecordMessageEvents);
                Assert.AreEqual(parentActivity, activity.Parent);
            }
        }

        /// <summary>
        /// Tests basic handler failure. Instructs the server to fail with resources exhausted and validates the created Activity.
        /// </summary>
        /// <param name="clientRequestFunc">The client request function.</param>
        /// <param name="statusCode">The status code to use for the failure. Defaults to ResourceExhausted.</param>
        /// <param name="validateErrorDescription">if set to <c>true</c> [validate error description].</param>
        /// <param name="serverUriString">An alternate server URI string.</param>
        /// <returns>
        /// A Task.
        /// </returns>
        private async Task TestHandlerFailure(
            Func<Foobar.FoobarClient, Task> clientRequestFunc,
            StatusCode statusCode = StatusCode.ResourceExhausted,
            bool validateErrorDescription = true,
            string serverUriString = null)
        {
            using var activityListener = new InterceptorActivityListener();
            var client = FoobarService.ConstructRpcClient(
                serverUriString ?? foobarUri,
                new ClientTracingInterceptor(new ClientTracingInterceptorOptions { Propagator = new TraceContextPropagator() }),
                new List<Metadata.Entry>
                {
                    new Metadata.Entry(FoobarService.RequestHeaderFailWithStatusCode, statusCode.ToString()),
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
            ValidateCommonActivityTags(activity, statusCode, false);

            if (validateErrorDescription)
            {
                Assert.IsTrue(activity.TagObjects.Any(t => t.Key == SemanticConventions.AttributeOtelStatusDescription && ((string)t.Value).Contains("fubar")));
            }
        }

        /// <summary>
        /// Tests for Activity cancellation when the handler is disposed before completing the RPC.
        /// </summary>
        /// <param name="clientRequestAction">The client request action.</param>
        private void TestActivityIsCancelledWhenHandlerDisposed(Action<Foobar.FoobarClient> clientRequestAction)
        {
            using var activityListener = new InterceptorActivityListener();
            var client = FoobarService.ConstructRpcClient(foobarUri, new ClientTracingInterceptor(new ClientTracingInterceptorOptions { Propagator = new TraceContextPropagator() }));
            clientRequestAction(client);

            var activity = activityListener.Activity;
            ValidateCommonActivityTags(activity, StatusCode.Cancelled, false);
        }
    }
}
