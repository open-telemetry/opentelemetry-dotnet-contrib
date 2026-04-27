// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using Xunit;

namespace OpenTelemetry.Instrumentation.Wcf.Tests;

public class TelemetryEndpointBehaviorTests
{
    [Fact]
    public void ApplyClientBehaviorToClientRuntime_WithNullActionOperation_DoesNotThrow()
    {
        // Arrange
        var contract = ContractDescription.GetContract(typeof(IServiceContract));
        var endpoint = new ServiceEndpoint(contract, new BasicHttpBinding(), new EndpointAddress("http://localhost/dummy"));

        endpoint.EndpointBehaviors.Add(new InjectNullActionOperationBehavior());
        endpoint.EndpointBehaviors.Add(new TelemetryEndpointBehavior());

        var factory = new ChannelFactory<IServiceContract>(endpoint);

        try
        {
            var exception = Record.Exception(factory.Open);
            Assert.Null(exception);
        }
        finally
        {
            factory.Abort();
        }
    }

    private sealed class InjectNullActionOperationBehavior : IEndpointBehavior
    {
        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
            // No-op
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            var nullActionOp = new ClientOperation(clientRuntime, "NullActionOperation", null);
            clientRuntime.ClientOperations.Add(nullActionOp);
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            // No-op
        }

        public void Validate(ServiceEndpoint endpoint)
        {
            // No-op
        }
    }
}
