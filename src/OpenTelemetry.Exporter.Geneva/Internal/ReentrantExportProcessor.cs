// <copyright file="ReentrantExportProcessor.cs" company="OpenTelemetry Authors">
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
