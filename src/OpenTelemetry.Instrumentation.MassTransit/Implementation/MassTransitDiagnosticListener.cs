// <copyright file="MassTransitDiagnosticListener.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.MassTransit.Implementation;

internal class MassTransitDiagnosticListener : ListenerHandler
{
    internal static readonly AssemblyName AssemblyName = typeof(MassTransitDiagnosticListener).Assembly.GetName();
    internal static readonly string ActivitySourceName = AssemblyName.Name;
    internal static readonly Version Version = AssemblyName.Version;
    internal static readonly ActivitySource ActivitySource = new ActivitySource(ActivitySourceName, Version.ToString());

    private readonly MassTransitInstrumentationOptions options;

    public MassTransitDiagnosticListener(string name, MassTransitInstrumentationOptions options)
        : base(name)
    {
        this.options = options;
    }

    public override void OnStartActivity(Activity activity, object payload)
    {
        // By this time, samplers have already run and
        // activity.IsAllDataRequested populated accordingly.

        if (Sdk.SuppressInstrumentation)
        {
            return;
        }

        if (activity.IsAllDataRequested)
        {
            if (this.options.TracedOperations != null && !this.options.TracedOperations.Contains(activity.OperationName))
            {
                MassTransitInstrumentationEventSource.Log.RequestIsFilteredOut(activity.OperationName);
                activity.IsAllDataRequested = false;
                return;
            }

            activity.DisplayName = this.GetDisplayName(activity);

            ActivityInstrumentationHelper.SetActivitySourceProperty(activity, ActivitySource);
            ActivityInstrumentationHelper.SetKindProperty(activity, this.GetActivityKind(activity));

            try
            {
                this.options.EnrichWithRequestPayload?.Invoke(activity, payload);
            }
            catch (Exception ex)
            {
                this.options.EnrichWithException(activity, ex);
            }
        }
    }

    public override void OnStopActivity(Activity activity, object payload)
    {
        if (activity.IsAllDataRequested)
        {
            try
            {
                this.TransformMassTransitTags(activity);

                this.options.EnrichWithResponsePayload?.Invoke(activity, payload);
            }
            catch (Exception ex)
            {
                this.options.EnrichWithException(activity, ex);
            }
        }
    }

    private string GetDisplayName(Activity activity)
    {
        return activity.OperationName switch
        {
            OperationName.Transport.Send => DisplayNameHelper.GetSendOperationDisplayName(this.GetTag(activity.Tags, TagName.PeerAddress)),
            OperationName.Transport.Receive => DisplayNameHelper.GetReceiveOperationDisplayName(this.GetTag(activity.Tags, TagName.PeerAddress)),
            OperationName.Consumer.Consume => DisplayNameHelper.GetConsumeOperationDisplayName(this.GetTag(activity.Tags, TagName.ConsumerType)),
            OperationName.Consumer.Handle => DisplayNameHelper.GetHandleOperationDisplayName(this.GetTag(activity.Tags, TagName.PeerAddress)),
            _ => activity.DisplayName,
        };
    }

    private ActivityKind GetActivityKind(Activity activity)
    {
        return activity.OperationName switch
        {
            OperationName.Transport.Send => ActivityKind.Producer,
            OperationName.Transport.Receive => ActivityKind.Consumer,
            OperationName.Consumer.Consume => ActivityKind.Internal,
            OperationName.Consumer.Handle => ActivityKind.Internal,
            _ => activity.Kind,
        };
    }

    private void TransformMassTransitTags(Activity activity)
    {
        if (activity.OperationName == OperationName.Transport.Send)
        {
            this.ProcessHostInfo(activity);

            this.RenameTag(activity, TagName.MessageId, SemanticConventions.AttributeMessagingMessageId);
            this.RenameTag(activity, TagName.ConversationId, SemanticConventions.AttributeMessagingConversationId);
            this.RenameTag(activity, TagName.InitiatorId, MassTransitSemanticConventions.AttributeMessagingMassTransitInitiatorId);
            this.RenameTag(activity, TagName.CorrelationId, MassTransitSemanticConventions.AttributeMessagingMassTransitCorrelationId);

            activity.SetTag(TagName.SourceAddress, null);
        }
        else if (activity.OperationName == OperationName.Transport.Receive)
        {
            this.ProcessHostInfo(activity);

            this.RenameTag(activity, TagName.MessageId, SemanticConventions.AttributeMessagingMessageId);
            this.RenameTag(activity, TagName.ConversationId, SemanticConventions.AttributeMessagingConversationId);
            this.RenameTag(activity, TagName.InitiatorId, MassTransitSemanticConventions.AttributeMessagingMassTransitInitiatorId);
            this.RenameTag(activity, TagName.CorrelationId, MassTransitSemanticConventions.AttributeMessagingMassTransitCorrelationId);

            activity.SetTag(TagName.MessageId, null);

            activity.SetTag(TagName.MessageTypes, null);
            activity.SetTag(TagName.SourceAddress, null);
            activity.SetTag(TagName.SourceHostMachine, null);
        }
        else if (activity.OperationName == OperationName.Consumer.Consume)
        {
            this.RenameTag(activity, TagName.ConsumerType, MassTransitSemanticConventions.AttributeMessagingMassTransitConsumerType);
        }
        else if (activity.OperationName == OperationName.Consumer.Handle)
        {
        }

        activity.SetTag(TagName.SpanKind, null);
        activity.SetTag(TagName.PeerService, null);
        activity.SetTag(TagName.PeerAddress, null);
        activity.SetTag(TagName.PeerHost, null);
    }

    private void ProcessHostInfo(Activity activity)
    {
        if (Uri.TryCreate(activity.GetTagValue(TagName.DestinationAddress).ToString(), UriKind.Absolute, out var destinationAddress))
        {
            activity.SetTag(SemanticConventions.AttributeMessagingSystem, destinationAddress.Scheme);
            activity.SetTag(SemanticConventions.AttributeMessagingDestination, destinationAddress.LocalPath);

            var uriHostNameType = Uri.CheckHostName(destinationAddress.Host);
            if (uriHostNameType == UriHostNameType.IPv4 || uriHostNameType == UriHostNameType.IPv6)
            {
                activity.SetTag(SemanticConventions.AttributeNetPeerIp, destinationAddress.Host);
            }
            else
            {
                activity.SetTag(SemanticConventions.AttributeNetPeerName, destinationAddress.Host);
            }

            if (destinationAddress.Port > 0)
            {
                activity.SetTag(SemanticConventions.AttributeNetPeerPort, destinationAddress.Port);
            }

            activity.SetTag(TagName.DestinationAddress, null);
        }
    }

    private string GetTag(IEnumerable<KeyValuePair<string, string>> tags, string tagName)
    {
        var tag = tags.SingleOrDefault(kv => kv.Key == tagName);
        return tag.Value;
    }

    private void RenameTag(Activity activity, string oldTagName, string newTagName)
    {
        activity.SetTag(newTagName, activity.GetTagValue(oldTagName));
        activity.SetTag(oldTagName, null);
    }
}
