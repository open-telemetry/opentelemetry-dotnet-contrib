// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Fabric;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;
using Xunit;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

public class ServiceFabricRemotingUtilsTests
{
    [Fact]
    public void GetServerAddress_WhenResolvedServicePartitionGetterThrows_ReturnsNull()
    {
        ThrowingPartitionClient client = new ThrowingPartitionClient();

        string? result = ServiceFabricRemotingUtils.GetServerAddress(client);

        Assert.Null(result);
    }

    [Fact]
    public void GetServerAddress_WhenResolvedServicePartitionIsNull_ReturnsNull()
    {
        NullPartitionClient client = new NullPartitionClient();

        string? result = ServiceFabricRemotingUtils.GetServerAddress(client);

        Assert.Null(result);
    }

    [Fact]
    public void GetServerAddress_WhenResolvedServicePartitionIsPopulated_ReturnsAbsoluteUri()
    {
        Uri serviceName = new Uri("fabric:/MyApp/MyService");
        ResolvedServicePartition partition = CreatePartitionWithServiceName(serviceName);
        PopulatedPartitionClient client = new PopulatedPartitionClient(partition);

        string? result = ServiceFabricRemotingUtils.GetServerAddress(client);

        Assert.Equal(serviceName.AbsoluteUri, result);
    }

    // ResolvedServicePartition has only internal constructors and no public setter for ServiceName.
    // For the positive-path test we bypass the constructor and set the Uri field via reflection.
    // Isolated here so the production code stays reflection-free.
    private static ResolvedServicePartition CreatePartitionWithServiceName(Uri serviceName)
    {
#pragma warning disable SYSLIB0050 // FormatterServices.GetUninitializedObject is obsolete
        ResolvedServicePartition partition = (ResolvedServicePartition)FormatterServices.GetUninitializedObject(typeof(ResolvedServicePartition));
#pragma warning restore SYSLIB0050

        FieldInfo? uriField = null;
        foreach (FieldInfo field in typeof(ResolvedServicePartition).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
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

        public string ListenerName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public ResolvedServiceEndpoint Endpoint { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public Task<IServiceRemotingResponseMessage> RequestResponseAsync(IServiceRemotingRequestMessage requestMessage) => throw new NotImplementedException();

        public void SendOneWay(IServiceRemotingRequestMessage requestMessage) => throw new NotImplementedException();
    }

    private sealed class NullPartitionClient : IServiceRemotingClient
    {
        public ResolvedServicePartition ResolvedServicePartition { get => null!; set => throw new NotImplementedException(); }

        public string ListenerName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public ResolvedServiceEndpoint Endpoint { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public Task<IServiceRemotingResponseMessage> RequestResponseAsync(IServiceRemotingRequestMessage requestMessage) => throw new NotImplementedException();

        public void SendOneWay(IServiceRemotingRequestMessage requestMessage) => throw new NotImplementedException();
    }

    private sealed class PopulatedPartitionClient : IServiceRemotingClient
    {
        private readonly ResolvedServicePartition partition;

        public PopulatedPartitionClient(ResolvedServicePartition partition)
        {
            this.partition = partition;
        }

        public ResolvedServicePartition ResolvedServicePartition { get => this.partition; set => throw new NotImplementedException(); }

        public string ListenerName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public ResolvedServiceEndpoint Endpoint { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public Task<IServiceRemotingResponseMessage> RequestResponseAsync(IServiceRemotingRequestMessage requestMessage) => throw new NotImplementedException();

        public void SendOneWay(IServiceRemotingRequestMessage requestMessage) => throw new NotImplementedException();
    }
}
