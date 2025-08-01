// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.Runtime;
using Amazon.Runtime.Identity;
using Amazon.Runtime.Internal;
using Amazon.Runtime.Internal.Auth;
using Amazon.Runtime.Internal.Transform;
using Amazon.Runtime.Internal.UserAgent;
using Amazon.Runtime.Internal.Util;

namespace OpenTelemetry.Instrumentation.AWS.Tests.Tools;

internal class MockRequestContext : IRequestContext
{
    public MockRequestContext(IClientConfig clientConfig)
    {
        this.ClientConfig = clientConfig;
    }

    public AmazonWebServiceRequest OriginalRequest { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public string RequestName => throw new NotImplementedException();

    public IMarshaller<IRequest, AmazonWebServiceRequest> Marshaller => throw new NotImplementedException();

    public ResponseUnmarshaller Unmarshaller => throw new NotImplementedException();

    public InvokeOptionsBase Options => throw new NotImplementedException();

    public RequestMetrics Metrics => throw new NotImplementedException();

    public ISigner Signer { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public BaseIdentity Identity { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public IClientConfig ClientConfig { get; }

    public IRequest Request { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public bool IsSigned { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public bool IsAsync => throw new NotImplementedException();

    public int Retries { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public CapacityManager.CapacityType LastCapacityType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public int EndpointDiscoveryRetries { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public CancellationToken CancellationToken => throw new NotImplementedException();

    public MonitoringAPICallAttempt CSMCallAttempt { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public MonitoringAPICallEvent CSMCallEvent { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public IServiceMetadata ServiceMetaData => throw new NotImplementedException();

    public bool CSMEnabled => throw new NotImplementedException();

    public bool IsLastExceptionRetryable { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public Guid InvocationId => throw new NotImplementedException();

    public IDictionary<string, object> ContextAttributes => throw new NotImplementedException();

    public IHttpRequestStreamHandle RequestStreamHandle { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public UserAgentDetails UserAgentDetails => throw new NotImplementedException();
}
