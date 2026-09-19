// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Policies;

namespace OpenTelemetry.DynamicControl.Tests;

internal static class PolicyKeyTestHelper
{
    public static PolicyKey Key(string policyType, string policyId) =>
        new(Type(policyType), Id(policyId));

    public static PolicyType Type(string value) => new(value);

    public static PolicyId Id(string value) => new(value);
}
