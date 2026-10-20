// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Fabric;
#endif
using System.Runtime.InteropServices;
using ServiceFabric.Mocks;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

[Collection(ServiceFabricRemotingCollectionDefinition.Name)]
public class RemotingProviderAttributeTests
{
    private const string NativeRuntimeRequiredMessage = "Creating a Service Fabric remoting listener requires the native Service Fabric runtime, which is only available on Windows and is not installed.";

    private static readonly Lazy<bool> IsNativeRuntimeInstalled = new(GetIsNativeRuntimeInstalled);

    [Fact]
    public void ServiceRemotingProvider_CreatesAV2Listener()
    {
        var provider = new TraceContextEnrichedServiceRemotingProviderAttribute();

        var listeners = provider.CreateServiceRemotingListeners();

        Assert.Equal("V2Listener", Assert.Single(listeners).Key);
    }

    [Fact]
    public void ServiceRemotingProvider_ListenerFactoryCreatesAListener()
    {
        Assert.SkipUnless(IsNativeRuntimeInstalled.Value, NativeRuntimeRequiredMessage);

        var provider = new TraceContextEnrichedServiceRemotingProviderAttribute();
        var serviceContext = MockStatefulServiceContextFactory.Default;
        var reliableStateManager = new MockReliableStateManager();
        var service = new MyTestStatefulService(serviceContext, reliableStateManager);
        var listenerFactory = Assert.Single(provider.CreateServiceRemotingListeners()).Value;

        var listener = listenerFactory(serviceContext, service);

        Assert.NotNull(listener);
    }

    [Fact]
    public void ServiceRemotingProvider_CreatesAnInstrumentedClientFactory()
    {
        var provider = new TraceContextEnrichedServiceRemotingProviderAttribute();

        var clientFactory = provider.CreateServiceRemotingClientFactoryV2(callbackMessageHandler: null);

        Assert.IsType<TraceContextEnrichedServiceRemotingClientFactoryAdapter>(clientFactory);
    }

    [Fact]
    public void ActorRemotingProvider_CreatesAV2Listener()
    {
        var provider = new TraceContextEnrichedActorRemotingProviderAttribute();

        var listeners = provider.CreateServiceRemotingListeners();

        Assert.Equal("V2Listener", Assert.Single(listeners).Key);
    }

    [Fact]
    public void ActorRemotingProvider_ListenerFactoryCreatesAListener()
    {
        Assert.SkipUnless(IsNativeRuntimeInstalled.Value, NativeRuntimeRequiredMessage);

        var provider = new TraceContextEnrichedActorRemotingProviderAttribute();
        var actorService = MockActorServiceFactory.CreateActorServiceForActor<MyTestActor>();
        var listenerFactory = Assert.Single(provider.CreateServiceRemotingListeners()).Value;

        var listener = listenerFactory(actorService);

        Assert.NotNull(listener);
    }

    [Fact]
    public void ActorRemotingProvider_CreatesAnInstrumentedClientFactory()
    {
        var provider = new TraceContextEnrichedActorRemotingProviderAttribute();

        var clientFactory = provider.CreateServiceRemotingClientFactory(callbackMessageHandler: null);

        Assert.IsType<TraceContextEnrichedServiceRemotingClientFactoryAdapter>(clientFactory);
    }

    private static bool GetIsNativeRuntimeInstalled()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        // The Service Fabric runtime installs FabricCommon.dll and adds its directory to the PATH,
        // so try to load it the same way the System.Fabric P/Invokes will.
        const string LibraryName = "FabricCommon.dll";

#if NET
        if (NativeLibrary.TryLoad(LibraryName, typeof(FabricClient).Assembly, searchPath: null, out var handle))
        {
            NativeLibrary.Free(handle);
            return true;
        }

        return false;
#else
        var handle = Kernel32.LoadLibrary(LibraryName);

        if (handle != IntPtr.Zero)
        {
            Kernel32.FreeLibrary(handle);
            return true;
        }

        return false;
#endif
    }

#if !NET
    private static class Kernel32
    {
#pragma warning disable CA5392 // Use DefaultDllImportSearchPaths attribute for P/Invokes
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadLibraryW", SetLastError = true)]
        internal static extern IntPtr LoadLibrary(string lpLibFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeLibrary(IntPtr hLibModule);
#pragma warning restore CA5392 // Use DefaultDllImportSearchPaths attribute for P/Invokes
    }
#endif
}
