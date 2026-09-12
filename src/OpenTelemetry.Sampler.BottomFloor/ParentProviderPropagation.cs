// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET9_0_OR_GREATER
using System.Runtime.CompilerServices;
#else
using System.Reflection;
#endif
using OpenTelemetry.Logs;

namespace OpenTelemetry.Sampler.BottomFloor;

/// <summary>
/// Forwards a <see cref="BaseProvider"/> to an exporter that the SDK cannot
/// reach on its own.
/// <para/>
/// The SDK sets <c>ParentProvider</c> on the exporter its processor owns, and
/// exporters read it to resolve the <c>Resource</c> they stamp on exported data.
/// A decorating exporter breaks that chain: the SDK sets the property on the
/// decorator, and the exporter it wraps is left with none. Without this
/// forwarding, wrapping any resource-aware exporter (OTLP among them) would fail
/// at export time.
/// <para/>
/// The setter is internal to the SDK and there is no supported alternative, so it
/// is reached directly. On .NET 9 and later that is an <c>UnsafeAccessor</c>,
/// which gained support for generic types in .NET 9 and so can bind a member of
/// <c>BaseExporter&lt;T&gt;</c>. Earlier targets use reflection. Either way the
/// cost is paid once per exporter, never on the per-record path.
/// <para/>
/// Both mechanisms depend on an SDK implementation detail. If neither can be
/// reached the wrapped exporter keeps the provider it already has, which for a
/// decorated exporter is none: the SDK assigns the property only to the
/// decorator. A resource-aware exporter that dereferences its provider will
/// therefore fail at export time, so a binding failure is a real degradation
/// rather than a benign one.
/// </summary>
internal static class ParentProviderPropagation
{
#if !NET9_0_OR_GREATER
    private static readonly Action<BaseExporter<LogRecord>, BaseProvider>? Setter = TryBindSetter();
#endif

    private static bool unsupported;

    /// <summary>
    /// Forwards <paramref name="provider"/> to <paramref name="exporter"/>.
    /// </summary>
    /// <param name="exporter">The exporter to receive the provider.</param>
    /// <param name="provider">The provider to forward.</param>
    /// <returns>
    /// <see langword="true"/> if the provider was forwarded; <see langword="false"/>
    /// if the SDK's setter could not be reached, in which case the wrapped exporter
    /// keeps the provider it already has.
    /// </returns>
    public static bool TrySet(BaseExporter<LogRecord> exporter, BaseProvider provider)
    {
        if (unsupported)
        {
            return false;
        }

#if NET9_0_OR_GREATER
        try
        {
            // An UnsafeAccessor binds when it is first called rather than when it
            // is declared, so this call is both the binding and the probe.
            Accessor<LogRecord>.SetParentProvider(exporter, provider);
            return true;
        }
        catch (MissingMethodException)
        {
            unsupported = true;
            return false;
        }
#else
        var setter = Setter;
        if (setter == null)
        {
            unsupported = true;
            return false;
        }

        setter(exporter, provider);
        return true;
#endif
    }

#if NET9_0_OR_GREATER
    /// <summary>
    /// Holds the accessor for <see cref="BaseExporter{T}"/>'s internal
    /// <c>ParentProvider</c> setter.
    /// <para/>
    /// The accessor has to live on a generic type whose parameters match the
    /// declaring type's. Declaring it as a generic <i>method</i> that takes a
    /// <see cref="BaseExporter{T}"/> compiles but fails at run time with
    /// <see cref="MissingMethodException"/>.
    /// </summary>
    /// <typeparam name="T">The type the exporter exports.</typeparam>
    private static class Accessor<T>
        where T : class
    {
        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_ParentProvider")]
        public static extern void SetParentProvider(BaseExporter<T> exporter, BaseProvider provider);
    }
#else
    private static Action<BaseExporter<LogRecord>, BaseProvider>? TryBindSetter()
    {
        try
        {
            var setMethod = typeof(BaseExporter<LogRecord>)
                .GetProperty(
                    "ParentProvider",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetMethod;

            if (setMethod == null)
            {
                return null;
            }

            return (Action<BaseExporter<LogRecord>, BaseProvider>)Delegate.CreateDelegate(
                typeof(Action<BaseExporter<LogRecord>, BaseProvider>), setMethod);
        }
        catch (Exception ex) when (ex is ArgumentException or MethodAccessException or AmbiguousMatchException)
        {
            return null;
        }
    }
#endif
}
