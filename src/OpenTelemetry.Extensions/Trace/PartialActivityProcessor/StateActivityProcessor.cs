// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Extensions.Trace.PartialActivityProcessor;

/// <summary>
/// Activity processor that exports LogRecord on Activity start, during it's lifetime as a heartbeat and on end.
/// </summary>
/// <remarks>
/// Add this processor *before* exporter related Activity processors.
/// </remarks>
public class StateActivityProcessor : BaseProcessor<Activity>
{
    private readonly ILogger logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StateActivityProcessor"/> class.
    /// </summary>
    /// <param name="logger">Logger used to send logs.</param>
    public StateActivityProcessor(ILogger logger)
    {
#if NET
        ArgumentNullException.ThrowIfNull(logger);
#else
#endif
        this.logger = logger ?? throw new ArgumentOutOfRangeException(nameof(logger));
    }

    /// <inheritdoc />
    public override void OnStart(Activity data)
    {
        using (this.logger.BeginScope(GetStartLogRecordAttributes()))
        {
            this.logger.LogInformation(
                SpecHelper.Json(new TracesData(data, TracesData.Signal.Start)));
        }
    }

    /// <inheritdoc />
    public override void OnEnd(Activity data)
    {
        using (this.logger.BeginScope(GetEndLogRecordAttributes()))
        {
            this.logger.LogInformation(
                SpecHelper.Json(new TracesData(data, TracesData.Signal.Stop)));
        }
    }

    private static List<KeyValuePair<string, object?>> GetEndLogRecordAttributes() =>
    [
        new("span.state", "ended")
    ];

    private static List<KeyValuePair<string, object?>> GetStartLogRecordAttributes() =>
    [
        new("span.state", "started")
    ];
}
