// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Fabric;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

public class ServiceFabricRemotingUtilsTests
{
    [Fact]
    public void GetServerAddress_WhenResolvedServicePartitionGetterThrows_ReturnsNull()
    {
        var client = new ThrowingPartitionClient();

        var result = ServiceFabricRemotingUtils.GetServerAddress(client);

        Assert.Null(result);
    }

    [Fact]
    public void GetServerAddress_WhenResolvedServicePartitionIsNull_ReturnsNull()
    {
        var client = new NullPartitionClient();

        var result = ServiceFabricRemotingUtils.GetServerAddress(client);

        Assert.Null(result);
    }

    [Fact]
    public void GetServerAddress_WhenResolvedServicePartitionIsPopulated_ReturnsAbsoluteUri()
    {
        var serviceName = new Uri("fabric:/MyApp/MyService");
        var partition = CreatePartitionWithServiceName(serviceName);
        var client = new PopulatedPartitionClient(partition);

        var result = ServiceFabricRemotingUtils.GetServerAddress(client);

        Assert.Equal(serviceName.AbsoluteUri, result);
    }

    // ResolvedServicePartition has only internal constructors and no public setter for ServiceName.
    // For the positive-path test we bypass the constructor and set the Uri field via reflection.
    // Isolated here so the production code stays reflection-free.
    private static ResolvedServicePartition CreatePartitionWithServiceName(Uri serviceName)
    {
#pragma warning disable SYSLIB0050 // FormatterServices.GetUninitializedObject is obsolete
        var partition = (ResolvedServicePartition)FormatterServices.GetUninitializedObject(typeof(ResolvedServicePartition));
#pragma warning restore SYSLIB0050

        FieldInfo? uriField = null;
        foreach (var field in typeof(ResolvedServicePartition).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (field.FieldType == typeof(Uri))
            {
                uriField = field;
                break;
            }
        }

        if (uriField == null)
        {
            throw new InvalidOperationException("Could not locate the Uri-typed backing field for ResolvedServicePartition.ServiceName. The SF SDK internals may have changed.");
        }

        uriField.SetValue(partition, serviceName);
        return partition;
    }

    private sealed class ThrowingPartitionClient : IServiceRemotingClient
    {
        public ResolvedServicePartition ResolvedServicePartition { get => throw new InvalidOperationException("not resolved"); set => throw new NotImplementedException(); }

        public string? ListenerName { get; set; }

        public ResolvedServiceEndpoint? Endpoint { get; set; }

        public Task<IServiceRemotingResponseMessage> RequestResponseAsync(IServiceRemotingRequestMessage requestMessage) => throw new NotImplementedException();

        public void SendOneWay(IServiceRemotingRequestMessage requestMessage) => throw new NotImplementedException();
    }

    private sealed class NullPartitionClient : IServiceRemotingClient
    {
        public ResolvedServicePartition? ResolvedServicePartition { get; set; }

        public string? ListenerName { get; set; }

        public ResolvedServiceEndpoint? Endpoint { get; set; }

        public Task<IServiceRemotingResponseMessage> RequestResponseAsync(IServiceRemotingRequestMessage requestMessage) => throw new NotImplementedException();

        public void SendOneWay(IServiceRemotingRequestMessage requestMessage) => throw new NotImplementedException();
    }

    private sealed class PopulatedPartitionClient : IServiceRemotingClient
    {
        public PopulatedPartitionClient(ResolvedServicePartition partition)
        {
            this.ResolvedServicePartition = partition;
        }

        public ResolvedServicePartition ResolvedServicePartition { get; set; }

        public string? ListenerName { get; set; }

        public ResolvedServiceEndpoint? Endpoint { get; set; }

        public Task<IServiceRemotingResponseMessage> RequestResponseAsync(IServiceRemotingRequestMessage requestMessage) => throw new NotImplementedException();

        public void SendOneWay(IServiceRemotingRequestMessage requestMessage) => throw new NotImplementedException();
    }
}
