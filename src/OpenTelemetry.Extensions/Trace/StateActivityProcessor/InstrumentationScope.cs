// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Extensions.Trace.StateActivityProcessor;

/// <summary>
/// Instrumentation scope per spec.
/// </summary>
public class InstrumentationScope
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InstrumentationScope"/> class.
    /// </summary>
    /// <param name="activity">Activity.</param>
    public InstrumentationScope(Activity activity)
    {
        Guard.ThrowIfNull(activity);

        this.Name = activity.Source.Name;

        this.Version = activity.Source.Version;

        foreach (var keyValuePair in activity.Tags)
        {
            KeyValue keyValue = new KeyValue
            {
                Key = keyValuePair.Key,
                Value = new AnyValue(keyValuePair.Value),
            };
            this.Attributes.Add(keyValue);
        }
    }

    /// <summary>
    /// Gets or sets the name of the instrumentation scope.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the version of the instrumentation scope.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets the attributes of the instrumentation scope.
    /// </summary>
    public Collection<KeyValue> Attributes { get; } = [];
}
