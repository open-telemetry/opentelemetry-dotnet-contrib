// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.Runtime.Internal.Auth;

namespace OpenTelemetry.Instrumentation.AWS.Tests;

internal class TestRequest(ParameterCollection parameters) : IRequest
{
    private readonly ParameterCollection parameters = parameters;

    public string RequestName => throw new NotImplementedException();

    public IDictionary<string, string> Headers => throw new NotImplementedException();

    public bool UseQueryString { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public IDictionary<string, string> Parameters => throw new NotImplementedException();

    public ParameterCollection ParameterCollection => this.parameters;

    public IDictionary<string, string> SubResources => throw new NotImplementedException();

    public string HttpMethod { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public Uri Endpoint { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public string ResourcePath { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public IDictionary<string, string> PathResources => throw new NotImplementedException();

    public int MarshallerVersion { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public byte[] Content { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public bool SetContentFromParameters { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public Stream ContentStream { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public long OriginalStreamPosition { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public string OverrideSigningServiceName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public string ServiceName => throw new NotImplementedException();

    public AmazonWebServiceRequest OriginalRequest => throw new NotImplementedException();

    public RegionEndpoint AlternateEndpoint { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public string HostPrefix { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public bool Suppress404Exceptions { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public AWS4SigningResult AWS4SignerResult { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public bool? DisablePayloadSigning { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public AWS4aSigningResult AWS4aSignerResult { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public bool UseChunkEncoding { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public string CanonicalResourcePrefix { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public bool UseSigV4 { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public SignatureVersion SignatureVersion { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public string AuthenticationRegion { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public string DeterminedSigningRegion { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public CoreChecksumAlgorithm SelectedChecksum { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public IDictionary<string, string> TrailingHeaders => throw new NotImplementedException();

    public bool UseDoubleEncoding { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public void AddPathResource(string key, string value)
    {
        throw new NotImplementedException();
    }

    public void AddSubResource(string subResource)
    {
        throw new NotImplementedException();
    }

    public void AddSubResource(string subResource, string value)
    {
        throw new NotImplementedException();
    }

    public string ComputeContentStreamHash()
    {
        throw new NotImplementedException();
    }

    public string GetHeaderValue(string headerName)
    {
        throw new NotImplementedException();
    }

    public bool HasRequestBody()
    {
        throw new NotImplementedException();
    }

    public bool IsRequestStreamRewindable()
    {
        throw new NotImplementedException();
    }

    public bool MayContainRequestBody()
    {
        throw new NotImplementedException();
    }
}
