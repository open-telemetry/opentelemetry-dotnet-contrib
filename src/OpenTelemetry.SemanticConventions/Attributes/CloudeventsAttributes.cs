// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from scripts/templates/SemanticConventionsAttributes.cs.j2</auto-generated>

#pragma warning disable CS1570 // XML comment has badly formed XML

using System;

namespace OpenTelemetry.SemanticConventions;

/// <summary>
/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
/// </summary>
public static class CloudeventsAttributes
{
    /// <summary>
    /// The <a href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md#id">event_id</a> uniquely identifies the event.
    /// </summary>
    public const string AttributeCloudeventsEventId = "cloudevents.event_id";

    /// <summary>
    /// The <a href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md#source-1">source</a> identifies the context in which an event happened.
    /// </summary>
    public const string AttributeCloudeventsEventSource = "cloudevents.event_source";

    /// <summary>
    /// The <a href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md#specversion">version of the CloudEvents specification</a> which the event uses.
    /// </summary>
    public const string AttributeCloudeventsEventSpecVersion = "cloudevents.event_spec_version";

    /// <summary>
    /// The <a href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md#subject">subject</a> of the event in the context of the event producer (identified by source).
    /// </summary>
    public const string AttributeCloudeventsEventSubject = "cloudevents.event_subject";

    /// <summary>
    /// The <a href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md#type">event_type</a> contains a value describing the type of event related to the originating occurrence.
    /// </summary>
    public const string AttributeCloudeventsEventType = "cloudevents.event_type";
}
