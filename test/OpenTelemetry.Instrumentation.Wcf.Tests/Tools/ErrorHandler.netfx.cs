// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;

namespace OpenTelemetry.Instrumentation.Wcf.Tests.Tools;

internal class ErrorHandler(EventWaitHandle handle, Action<Exception> log) : IErrorHandler
{
    public bool HandleError(Exception error)
    {
        log(error);
        handle.Set();

        return true;
    }

    public void ProvideFault(Exception error, MessageVersion version, ref Message fault)
    {
    }
}

#endif
