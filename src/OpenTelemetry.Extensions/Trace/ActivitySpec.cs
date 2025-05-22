// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OpenTelemetry.Extensions.Trace;

/// <summary>
/// Activity per spec.
/// </summary>
public class ActivitySpec
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = new SnakeCaseNamingPolicy(),
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivitySpec"/> class.
    /// Creates an instance of <see cref="ActivitySpec"/> from an <see cref="Activity"/>.
    /// </summary>
    /// <param name="activity">Activity.</param>
    /// <param name="signal">Signal for which mapping is called.</param>
    public ActivitySpec(Activity activity, Signal signal)
    {
#if NET
        ArgumentNullException.ThrowIfNull(activity);
#else
        if (activity == null)
        {
            throw new ArgumentOutOfRangeException(nameof(activity));
        }
#endif

        this.Name = activity.DisplayName;
        this.ActivitySpecContext = new ActivitySpecContext(activity.Context);
        this.ParentId = activity.ParentSpanId == default
            ? string.Empty
            : activity.ParentSpanId.ToString();
        this.StartTime = FormatTimestamp(activity.StartTimeUtc);
        this.EndTime = signal == Signal.Heartbeat
            ? string.Empty
            : FormatTimestamp(activity.StartTimeUtc.Add(activity.Duration));
        this.StatusCode = activity.Status.ToString();
        this.StatusMessage = activity.StatusDescription ?? string.Empty;
        this.Attributes = activity.TagObjects
            .ToDictionary(tag => tag.Key, tag => tag.Value)!;
    }

    /// <summary>
    /// Signal for the activity.
    /// </summary>
    public enum Signal
    {
        /// <summary>
        /// Heartbeat signal.
        /// </summary>
        Heartbeat,

        /// <summary>
        /// Stop signal.
        /// </summary>
        Stop,
    }

    /// <summary>
    /// Gets or sets name of the activity.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets context of the activity.
    /// </summary>
    public ActivitySpecContext? ActivitySpecContext { get; set; }

    /// <summary>
    /// Gets or sets parent id of the activity.
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// Start time of the activity.
    /// </summary>
    public string? StartTime { get; set; }

    /// <summary>
    /// Gets or sets end time of the activity.
    /// </summary>
    public string? EndTime { get; set; }

    /// <summary>
    /// Gets or sets status code of the activity.
    /// </summary>
    public string? StatusCode { get; set; }

    /// <summary>
    /// Gets or sets status message of the activity.
    /// </summary>
    public string? StatusMessage { get; set; }

    /// <summary>
    /// Gets or sets attributes of the activity.
    /// </summary>
    public Dictionary<string, object>? Attributes { get; }

    /// <summary>
    /// Gets or sets events of the activity.
    /// </summary>
    public Collection<ActivitySpecEvent>? Events { get; }

    /// <summary>
    /// Format timestamp for UTC representation.
    /// </summary>
    /// <param name="dateTime">Datetime from activity.</param>
    /// <returns>Formatted timestamp in UTC representation.</returns>
    public static string FormatTimestamp(DateTime dateTime) =>
        dateTime.ToString(
            "yyyy-MM-dd HH:mm:ss.ffffff",
            CultureInfo.InvariantCulture) + " +0000 UTC";

    /// <summary>
    /// Serialize the activity spec to JSON string.
    /// </summary>
    /// <param name="activitySpec">Activity spec.</param>
    /// <returns>JSON representing activity per spec.</returns>
    public static string Json(ActivitySpec activitySpec) =>
        JsonSerializer.Serialize(activitySpec, JsonSerializerOptions);
}
