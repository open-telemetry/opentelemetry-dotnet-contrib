// <copyright file="TestHttpWorkerRequest.cs" company="OpenTelemetry Authors">
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
using System.Web;

namespace OpenTelemetry.Instrumentation.AspNet.Tests;

internal class TestHttpWorkerRequest : HttpWorkerRequest
{
    public override void EndOfRequest()
    {
        throw new NotImplementedException();
    }

    public override void FlushResponse(bool finalFlush)
    {
        throw new NotImplementedException();
    }

    public override string GetHttpVerbName()
    {
        throw new NotImplementedException();
    }

    public override string GetHttpVersion()
    {
        throw new NotImplementedException();
    }

    public override string GetLocalAddress()
    {
        throw new NotImplementedException();
    }

    public override int GetLocalPort()
    {
        throw new NotImplementedException();
    }

    public override string GetQueryString()
    {
        throw new NotImplementedException();
    }

    public override string GetRawUrl()
    {
        throw new NotImplementedException();
    }

    public override string GetRemoteAddress()
    {
        throw new NotImplementedException();
    }

    public override int GetRemotePort()
    {
        throw new NotImplementedException();
    }

    public override string GetUriPath()
    {
        throw new NotImplementedException();
    }

    public override void SendKnownResponseHeader(int index, string value)
    {
        throw new NotImplementedException();
    }

    public override void SendResponseFromFile(string filename, long offset, long length)
    {
        throw new NotImplementedException();
    }

    public override void SendResponseFromFile(IntPtr handle, long offset, long length)
    {
        throw new NotImplementedException();
    }

    public override void SendResponseFromMemory(byte[] data, int length)
    {
        throw new NotImplementedException();
    }

    public override void SendStatus(int statusCode, string statusDescription)
    {
        throw new NotImplementedException();
    }

    public override void SendUnknownResponseHeader(string name, string value)
    {
        throw new NotImplementedException();
    }
}
