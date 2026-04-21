// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using Xunit;

namespace OpenTelemetry.Resources.Azure.Tests;

public class AzureVmMetaDataRequestorTests
{
    private const int MessageSizeLimit = 4 * 1024 * 1024;

    [Fact]
    public void GetAzureVmMetaData_HttpResponseWithoutContent_ReturnsCorrectResult()
    {
        // Arrange
        using var httpResponse = new HttpResponseMessage() { Content = null };
        var cancellationToken = CancellationToken.None;

        using var handler = new StubHttpMessageHandler(httpResponse);
        using var httpClient = new HttpClient(handler);

        // Act
        var actual = AzureVmMetaDataRequestor.GetAzureVmMetaData(httpClient, cancellationToken);

        // Assert
        Assert.Null(actual);
    }

    [Fact]
    public void GetAzureVmMetaData_ResponseWithinLimits_ReturnsFullContent()
    {
        // Arrange
        var json =
            """
            {
              "azEnvironment": "AzurePublicCloud",
              "customData": "",
              "evictionPolicy": "",
              "isHostCompatibilityLayerVm": "false",
              "licenseType": "",
              "location": "eastus",
              "name": "myvm-01",
              "offer": "0001-com-ubuntu-server-focal",
              "osProfile": {
                "adminUsername": "azureuser",
                "computerName": "myvm-01",
                "disablePasswordAuthentication": "true"
              },
              "osType": "Linux",
              "placementGroupId": "",
              "plan": {
                "name": "",
                "product": "",
                "publisher": ""
              },
              "platformFaultDomain": "0",
              "platformSubFaultDomain": "",
              "platformUpdateDomain": "0",
              "priority": "",
              "provider": "Microsoft.Compute",
              "publicKeys": [
                {
                  "keyData": "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQC7... user@host",
                  "path": "/home/azureuser/.ssh/authorized_keys"
                }
              ],
              "publisher": "canonical",
              "resourceGroupName": "rg-demo",
              "resourceId": "/subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/rg-demo/providers/Microsoft.Compute/virtualMachines/myvm-01",
              "securityProfile": {
                "secureBootEnabled": "true",
                "virtualTpmEnabled": "true"
              },
              "sku": "20_04-lts-gen2",
              "storageProfile": {
                "dataDisks": [],
                "imageReference": {
                  "id": "",
                  "offer": "0001-com-ubuntu-server-focal",
                  "publisher": "canonical",
                  "sku": "20_04-lts-gen2",
                  "version": "latest"
                },
                "osDisk": {
                  "caching": "ReadWrite",
                  "createOption": "FromImage",
                  "diskSizeGB": "30",
                  "managedDisk": {
                    "id": "/subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/rg-demo/providers/Microsoft.Compute/disks/myvm-01_OsDisk_1_abcdef"
                  },
                  "name": "myvm-01_OsDisk_1_abcdef",
                  "osType": "Linux",
                  "vhd": ""
                }
              },
              "subscriptionId": "11111111-2222-3333-4444-555555555555",
              "tags": "env:dev;owner:team-a",
              "tagsList": [
                { "name": "env", "value": "dev" },
                { "name": "owner", "value": "team-a" }
              ],
              "userData": "",
              "version": "20.04.202401300",
              "vmId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "vmScaleSetName": "",
              "vmSize": "Standard_D2s_v5",
              "zone": "1"
            }
            """;

        var cancellationToken = CancellationToken.None;

        using var httpResponse = new HttpResponseMessage()
        {
            Content = new StringContent(json),
        };

        using var handler = new StubHttpMessageHandler(httpResponse);
        using var httpClient = new HttpClient(handler);

        // Act
        var actual = AzureVmMetaDataRequestor.GetAzureVmMetaData(httpClient, cancellationToken);

        // Assert
        Assert.NotNull(actual);
        Assert.Equal("Standard_D2s_v5", actual.VmSize);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1024)]
    [InlineData(2048)]
    public void GetAzureVmMetaData_ContentExceedsLimit_Throws(int excess)
    {
        // Arrange
        var content = @"{""vmSize"": """ + new string('C', MessageSizeLimit + excess) + @"""}";
        var cancellationToken = CancellationToken.None;

        using var httpResponse = new HttpResponseMessage()
        {
            Content = new StringContent(content),
        };

        using var handler = new StubHttpMessageHandler(httpResponse);
        using var httpClient = new HttpClient(handler);

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => HttpClientHelpers.GetResponseBodyAsString(httpResponse, cancellationToken));
    }

    [Fact]
    public void GetAzureVmMetaData_EmptyDocument_ReturnsResponse()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        using var httpResponse = new HttpResponseMessage()
        {
            Content = new StringContent("{}"),
        };

        using var handler = new StubHttpMessageHandler(httpResponse);
        using var httpClient = new HttpClient(handler);

        // Act
        var actual = AzureVmMetaDataRequestor.GetAzureVmMetaData(httpClient, cancellationToken);

        // Assert
        Assert.NotNull(actual);
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
#if NET
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) => response;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(response);
#else
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(response);
#endif
    }
}
