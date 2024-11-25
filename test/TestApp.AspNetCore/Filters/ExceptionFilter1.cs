// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Mvc.Filters;

namespace TestApp.AspNetCore.Filters;

#pragma warning disable CA1515
public class ExceptionFilter1 : IExceptionFilter
#pragma warning restore CA1515
{
    public void OnException(ExceptionContext context)
    {
        // test the behaviour when an application has two ExceptionFilters defined
    }
}
