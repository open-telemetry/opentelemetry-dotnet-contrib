// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Runtime.CompilerServices;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Sampler.BottomFloor;

/// <summary>
/// Retains a pooled <see cref="LogRecord"/> beyond the single enumeration visit
/// a batch exporter is given.
/// <para/>
/// A record delivered to a batch exporter is rented from the shared log-record
/// pool, and the batch enumerator returns it to that pool as it advances past
/// it, which clears its attributes and scopes and frees it for reuse by a
/// concurrent logging thread. A windowed sampler must hold selected records
/// until the window closes, so it cannot forward the pooled record itself.
/// <para/>
/// The SDK's own retaining components take a self-contained copy through the
/// internal <c>LogRecord.Copy()</c>, which returns a manually created record the
/// pool never reclaims. That method is not part of the public surface, so this
/// helper reaches it with an <c>UnsafeAccessor</c>. That attribute requires
/// .NET 8 or later, which every target framework of this package satisfies
/// (<c>net8.0</c> and <c>net10.0</c>); adding a <c>netstandard2.0</c> target
/// would require reintroducing a reflection fallback.
/// <para/>
/// The binding may fail against a future SDK, so support is probed
/// once up front and reported through <see cref="CanClone"/>. A caller must check
/// it before sampling: <c>UnsafeAccessor</c> resolves lazily and would otherwise
/// throw <see cref="MissingMethodException"/> from inside the export path.
/// </summary>
internal static class LogRecordRetention
{
    private static readonly bool CopySupported = FindCopyMethod() != null;

    /// <summary>
    /// Gets a value indicating whether a safe, self-contained copy can be taken.
    /// When <see langword="false"/> the SDK's copy primitive is missing and the
    /// caller must not sample, since it cannot retain a record beyond its visit.
    /// </summary>
    public static bool CanClone => CopySupported;

    /// <summary>
    /// Returns a self-contained copy of <paramref name="record"/> that the log
    /// record pool never reclaims, safe to hold and mutate until the window
    /// closes. Must be called while the record is still valid, during the
    /// enumeration visit that delivers it, and only when <see cref="CanClone"/>
    /// is <see langword="true"/>.
    /// </summary>
    /// <param name="record">The pooled record to retain.</param>
    /// <returns>A retained copy of the record.</returns>
    public static LogRecord Retain(LogRecord record) => CopySupported ? Copy(record) : record;

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Copy")]
    private static extern LogRecord Copy(LogRecord record);

    private static MethodInfo? FindCopyMethod()
    {
        try
        {
            var method = typeof(LogRecord).GetMethod(
                "Copy",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

            return method != null && method.ReturnType == typeof(LogRecord) ? method : null;
        }
        catch (AmbiguousMatchException)
        {
            return null;
        }
    }
}
