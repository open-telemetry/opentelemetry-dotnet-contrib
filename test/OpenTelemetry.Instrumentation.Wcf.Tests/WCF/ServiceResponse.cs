// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.Serialization;

namespace OpenTelemetry.Instrumentation.Wcf.Tests;

[DataContract]
#pragma warning disable CA1515 // Make class internal, public is needed for WCF
public class ServiceResponse
#pragma warning restore CA1515 // Make class internal, public is needed for WCF
{
    public ServiceResponse(string payload)
    {
        this.Payload = payload;
    }

    [DataMember]
    public string Payload { get; set; }
}
