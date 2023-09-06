// <copyright file="AspNetParentSpanCorrectorTests.netfx.cs" company="OpenTelemetry Authors">
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
using System.Collections.Specialized;
using System.Diagnostics;
using System.Reflection;
using OpenTelemetry.Instrumentation.Wcf.Implementation;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Wcf.Tests;

[Collection("WCF")]
public class AspNetParentSpanCorrectorTests
{
    [Fact]
    public void IncomingRequestHeadersAreOverwrittenWithAspNetParent()
    {
        var testSource = new ActivitySource("TestSource");
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource("TestSource")
            .AddWcfInstrumentation()
            .Build();

        var reflectedValues = typeof(AspNetParentSpanCorrector).GetField("ReflectedValues", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        Assert.False(reflectedValues == null, "The reflection-based bind to OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule failed. The AspNet telemetry has likely changed, and this assembly needs to be updated to match");

        using (var aspNetActivity = testSource.StartActivity("AspNetActivity"))
        {
            var context = new FakeHttpContext();

            var method = typeof(AspNetParentSpanCorrector).GetMethod("OnRequestStarted", BindingFlags.Static | BindingFlags.NonPublic);
            method.Invoke(null, new object[] { aspNetActivity, context });

            var headerVal = context.Request.Headers["traceparent"];
            Assert.Contains(aspNetActivity.TraceId.ToString(), headerVal);
            Assert.Contains(aspNetActivity.SpanId.ToString(), headerVal);
        }
    }

    private class FakeHttpContext
    {
        public FakeRequest Request { get; } = new FakeRequest();
    }

    private class FakeRequest
    {
        public NameValueCollection Headers { get; } = new NameValueCollection();
    }
}
#endif
