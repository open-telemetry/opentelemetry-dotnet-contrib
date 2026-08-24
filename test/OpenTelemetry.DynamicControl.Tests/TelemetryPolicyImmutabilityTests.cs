// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using OpenTelemetry.DynamicControl.Internal.Policies;

namespace OpenTelemetry.DynamicControl.Tests;

public class TelemetryPolicyImmutabilityTests
{
    private const BindingFlags DeclaredInstance =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    [Fact]
    public void PolicyTypes_AreDiscovered()
    {
        var types = GetPolicyTypes();

        Assert.Contains(typeof(TelemetryPolicy), types);
        Assert.Contains(typeof(TraceSamplingRatePolicy), types);
    }

    [Fact]
    public void EveryDeclaredProperty_IsGetOnly()
    {
        foreach (var type in GetPolicyTypes())
        {
            foreach (var property in type.GetProperties(DeclaredInstance))
            {
                var message =
                    $"{type.Name}.{property.Name} has a setter. Policy properties must be " +
                    "get-only and take their value from the constructor or a static factory. " +
                    "This also rejects 'init', which is writable during construction of a copy.";

                Assert.False(property.CanWrite, message);
            }
        }
    }

    [Fact]
    public void EveryDeclaredField_IsReadOnly()
    {
        foreach (var type in GetPolicyTypes())
        {
            foreach (var field in type.GetFields(DeclaredInstance))
            {
                var message =
                    $"{type.Name}.{field.Name} is not readonly. The store shares policy " +
                    "instances across snapshots and readers without synchronization, so no " +
                    "field may be assignable after construction. Auto-property backing " +
                    "fields appear here too, which is how 'init' accessors are caught.";

                Assert.True(field.IsInitOnly, message);
            }
        }
    }

    [Fact]
    public void EveryConcreteType_IsSealed()
    {
        foreach (var type in GetPolicyTypes())
        {
            if (type.IsAbstract)
            {
                continue;
            }

            var message =
                $"{type.Name} is a concrete policy type and must be sealed, so that the " +
                "immutability checked here cannot be undermined by a subtype.";

            Assert.True(type.IsSealed, message);
        }
    }

    [Fact]
    public void NoTypeSynthesizesValueEquality()
    {
        foreach (var type in GetPolicyTypes())
        {
            Assert.Null(type.GetMethod("op_Equality", BindingFlags.Public | BindingFlags.Static));
            Assert.Null(type.GetMethod("<Clone>$", DeclaredInstance));

            var equals = type.GetMethod("Equals", [typeof(object)]);
            var getHashCode = type.GetMethod("GetHashCode", Type.EmptyTypes);

            Assert.Equal(typeof(object), equals!.DeclaringType);
            Assert.Equal(typeof(object), getHashCode!.DeclaringType);
        }
    }

    [Fact]
    public void PoliciesWithIdenticalContent_AreNotEqual()
    {
        Assert.True(TraceSamplingRatePolicy.TryCreate("policy-1", "Policy one", 0.25, out var first, out _), "TryCreate should succeed for first policy");
        Assert.True(TraceSamplingRatePolicy.TryCreate("policy-1", "Policy one", 0.25, out var second, out _), "TryCreate should succeed for second policy");
        Assert.NotSame(first, second);
        Assert.False(first!.Equals(second), "Policies with identical content should not be equal. Identity is by reference");
        Assert.False(second!.Equals(first), "Policies with identical content should not be equal. Identity is by reference");
    }

    private static List<Type> GetPolicyTypes()
        => [.. typeof(TelemetryPolicy).Assembly
            .GetTypes()
            .Where(typeof(TelemetryPolicy).IsAssignableFrom)
            .OrderBy(t => t.FullName, StringComparer.Ordinal)];
}
