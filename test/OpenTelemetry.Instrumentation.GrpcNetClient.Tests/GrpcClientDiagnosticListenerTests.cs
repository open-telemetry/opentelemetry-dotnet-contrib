// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using OpenTelemetry.Instrumentation.GrpcNetClient;
using OpenTelemetry.Instrumentation.GrpcNetClient.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Grpc.Tests;

public class GrpcClientDiagnosticListenerTests
{
    public static TheoryData<string> RequestUris() =>
    [
        "http://localhost:1234/greet.Greeter/SayHello",
        "http://LOCALHOST/",
        "https://orders.default.svc.cluster.local/",
        "https://example.com./",
        "http://my-service:5000/",
        "http://my_service:5000/",
        "http://xn--bcher-kva.example/",
        "http://bücher.example/",
        "http://127.0.0.1:1234/greet.Greeter/SayHello",
        "http://10.0.0.5:8080/",
        "http://0.0.0.0/",
        "http://255.255.255.255/",
        "http://user:password@127.0.0.1/",
        "http://127.1/",
        "http://0x7f.1/",
        "http://0177.0.0.1/",
        "http://2130706433/",
        "http://1.2.3.4.5/",
        "http://999.1.1.1/",
        "http://1.2.3.4a/",
        "http://[::1]:5000/",
        "https://[::1]/",
        "http://[::]/",
        "http://[2001:db8::1]/",
        "http://[2001:DB8:0:0:0:0:0:1]/",
        "http://[::ffff:127.0.0.1]/",
        "http://[fe80::1%25eth0]:5000/",
        "http://[fe80::1%2511]/",
    ];

    [Theory]
    [MemberData(nameof(RequestUris))]
    [InlineData("unix:///tmp/grpc.sock")]
    public void IsIPAddressMatchesCheckHostName(string requestUri)
    {
        var uri = new Uri(requestUri);
        var expected = Uri.CheckHostName(uri.Host);

        var actual = GrpcClientDiagnosticListener.IsIPAddress(uri);

        Assert.Equal(expected is UriHostNameType.IPv4 or UriHostNameType.IPv6, actual);

        if (actual)
        {
            Assert.Equal(expected, uri.HostNameType);
        }
    }

    [Theory]
    [MemberData(nameof(RequestUris))]
    public void NetworkPeerTagsAreSetOnlyForIPAddresses(string requestUri)
    {
        var uri = new Uri(requestUri);
        var isIPAddress = Uri.CheckHostName(uri.Host) is UriHostNameType.IPv4 or UriHostNameType.IPv6;

        var listener = new GrpcClientDiagnosticListener(new GrpcClientTraceInstrumentationOptions());

        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request };

        using var activity = new Activity("Grpc.Net.Client.GrpcOut");
        activity.SetTag(GrpcTagHelper.GrpcMethodTagName, "/greet.Greeter/SayHello");
        activity.SetTag(GrpcTagHelper.GrpcStatusCodeTagName, "0");
        activity.Start();

        listener.OnStartActivity(activity, new { Request = request });

        Assert.Equal(uri.Host, activity.GetTagValue(SemanticConventions.AttributeServerAddress));
        Assert.Equal(uri.Port, activity.GetTagValue(SemanticConventions.AttributeServerPort));
        AssertNetworkPeerTags(activity, uri, isIPAddress);

        // Remove the tags so that the stop event is verified independently of the start event
        activity.SetTag(SemanticConventions.AttributeNetworkPeerAddress, null);
        activity.SetTag(SemanticConventions.AttributeNetworkPeerPort, null);

        listener.OnStopActivity(activity, new { Response = response });

        AssertNetworkPeerTags(activity, uri, isIPAddress);
        Assert.Equal("greet.Greeter/SayHello", activity.GetTagValue(SemanticConventions.AttributeRpcMethod));
        Assert.Equal("greet.Greeter/SayHello", activity.DisplayName);

        static void AssertNetworkPeerTags(Activity activity, Uri uri, bool isIPAddress)
        {
            if (isIPAddress)
            {
                Assert.Equal(uri.Host, activity.GetTagValue(SemanticConventions.AttributeNetworkPeerAddress));
                Assert.Equal(uri.Port, activity.GetTagValue(SemanticConventions.AttributeNetworkPeerPort));
            }
            else
            {
                Assert.Null(activity.GetTagValue(SemanticConventions.AttributeNetworkPeerAddress));
                Assert.Null(activity.GetTagValue(SemanticConventions.AttributeNetworkPeerPort));
            }
        }
    }

    [Theory]
    [InlineData("/greet.Greeter/SayHello")]
    [InlineData("greet.Greeter/SayHello")]
    [InlineData("//greet.Greeter/SayHello//")]
    [InlineData("/other")]
    [InlineData("other")]
    [InlineData("/")]
    [InlineData("")]
    public void GetTrimmedGrpcMethodReturnsTrimmedMethod(string grpcMethod)
    {
        // Use a copy so the same value is also looked up through a different string instance
        var other = new string(grpcMethod.ToCharArray());
        var expected = grpcMethod.Trim('/');

        var first = GrpcClientDiagnosticListener.GetTrimmedGrpcMethod(grpcMethod);
        var second = GrpcClientDiagnosticListener.GetTrimmedGrpcMethod(grpcMethod);
        var third = GrpcClientDiagnosticListener.GetTrimmedGrpcMethod(other);

        Assert.Equal(expected, first);
        Assert.Equal(expected, second);
        Assert.Equal(expected, third);

#if NET
        Assert.Same(first, second);
#endif
    }

    [Theory]
    [InlineData("/greet.Greeter/SayHello", "greet.Greeter/SayHello", "greet.Greeter/SayHello", null)]
    [InlineData("/other", "grpc", GrpcTagHelper.RpcMethodOther, GrpcTagHelper.GrpcMethodOther)]
    public void RpcMethodIsSetFromGrpcMethod(string grpcMethod, string expectedDisplayName, string expectedRpcMethod, string? expectedRpcMethodOriginal)
    {
        var listener = new GrpcClientDiagnosticListener(new GrpcClientTraceInstrumentationOptions());

        using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:1234/");

        // Repeat the call to also verify the values returned for the same grpc.method instance
        for (var i = 0; i < 2; i++)
        {
            using var activity = new Activity("Grpc.Net.Client.GrpcOut");
            activity.SetTag(GrpcTagHelper.GrpcMethodTagName, grpcMethod);
            activity.Start();

            listener.OnStartActivity(activity, new { Request = request });

            Assert.Equal(expectedDisplayName, activity.DisplayName);
            Assert.Equal(expectedRpcMethod, activity.GetTagValue(SemanticConventions.AttributeRpcMethod));
            Assert.Equal(expectedRpcMethodOriginal, activity.GetTagValue(SemanticConventions.AttributeRpcMethodOriginal));
            Assert.Null(activity.GetTagValue(GrpcTagHelper.GrpcMethodTagName));
        }
    }
}
