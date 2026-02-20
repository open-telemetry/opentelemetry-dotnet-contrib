// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Wcf;

internal static class WcfInstrumentationConstants
{
    public const string SoapMessageVersionTag = "soap.message_version";
    public const string SoapReplyActionTag = "soap.reply_action";
    public const string SoapViaTag = "soap.via";
    public const string WcfChannelSchemeTag = "wcf.channel.scheme";
    public const string WcfChannelPathTag = "wcf.channel.path";

    public const string WcfSystemValue = "dotnet_wcf";
}
