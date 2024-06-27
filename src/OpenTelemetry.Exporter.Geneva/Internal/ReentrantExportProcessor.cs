// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Linq.Expressions;
using System.Reflection;

namespace OpenTelemetry.Exporter.Geneva;

// This export processor exports without synchronization.
// Once OpenTelemetry .NET officially support this,
// we can get rid of this class.
// This is currently only used in ETW export, where we know
// that the underlying system is safe under concurrent calls.
internal class ReentrantExportProcessor<T> : BaseExportProcessor<T>
    where T : class
{
    static ReentrantExportProcessor()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var ctor = typeof(Batch<T>).GetConstructor(flags, null, new Type[] { typeof(T) }, null);
        var value = Expression.Parameter(typeof(T), null);
        var lambda = Expression.Lambda<Func<T, Batch<T>>>(Expression.New(ctor, value), value);
        CreateBatch = lambda.Compile();
    }

    public ReentrantExportProcessor(BaseExporter<T> exporter)
        : base(exporter)
    {
    }

    protected override void OnExport(T data)
    {
        this.exporter.Export(CreateBatch(data));
    }

    private static readonly Func<T, Batch<T>> CreateBatch;
}
