// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable disable

#pragma warning disable IDE0005 // Using directive is unnecessary.
using System;
using System.Diagnostics;
using System.Linq.Expressions;
#pragma warning restore IDE0005 // Using directive is unnecessary.

namespace OpenTelemetry.Instrumentation;

internal static class ActivityInstrumentationHelper
{
    internal static readonly Action<Activity, ActivityKind> SetKindProperty = CreateActivityKindSetter();
    internal static readonly Action<Activity, ActivitySource> SetActivitySourceProperty = CreateActivitySourceSetter();

    private static Action<Activity, ActivitySource> CreateActivitySourceSetter()
    {
        ParameterExpression instance = Expression.Parameter(typeof(Activity), "instance");
        ParameterExpression propertyValue = Expression.Parameter(typeof(ActivitySource), "propertyValue");
        var body = Expression.Assign(Expression.Property(instance, "Source"), propertyValue);
        return Expression.Lambda<Action<Activity, ActivitySource>>(body, instance, propertyValue).Compile();
    }

    private static Action<Activity, ActivityKind> CreateActivityKindSetter()
    {
        ParameterExpression instance = Expression.Parameter(typeof(Activity), "instance");
        ParameterExpression propertyValue = Expression.Parameter(typeof(ActivityKind), "propertyValue");
        var body = Expression.Assign(Expression.Property(instance, "Kind"), propertyValue);
        return Expression.Lambda<Action<Activity, ActivityKind>>(body, instance, propertyValue).Compile();
    }
}
