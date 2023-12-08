// <copyright file="TestRequestContext.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.Runtime.Internal.Auth;
using Amazon.Runtime.Internal.Transform;
using Amazon.Runtime.Internal.Util;

namespace OpenTelemetry.Instrumentation.AWS.Tests;

internal class TestRequestContext(AmazonWebServiceRequest originalRequest, IRequest request) : IRequestContext
{
    private readonly AmazonWebServiceRequest originalRequest = originalRequest;
    private IRequest request = request;

    public AmazonWebServiceRequest OriginalRequest => this.originalRequest;

    public string RequestName => throw new NotImplementedException();

    public IMarshaller<IRequest, AmazonWebServiceRequest> Marshaller => throw new NotImplementedException();

    public ResponseUnmarshaller Unmarshaller => throw new NotImplementedException();

    public InvokeOptionsBase Options => throw new NotImplementedException();

    public RequestMetrics Metrics => throw new NotImplementedException();

    public AbstractAWSSigner Signer => throw new NotImplementedException();

    public IClientConfig ClientConfig => throw new NotImplementedException();

    public ImmutableCredentials ImmutableCredentials { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public IRequest Request { get => this.request; set => this.request = value; }

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
}
