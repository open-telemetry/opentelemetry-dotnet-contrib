// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.Serialization;

namespace OpenTelemetry.Instrumentation.Wcf.Tests;

[DataContract]
public class ServiceRequest(string payload)
{
    [DataMember]
    public string Payload { get; set; } = payload;
}
