﻿// <copyright file="UnixDomainSocketEndPointTests.cs" company="OpenTelemetry Authors">
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
using System.Net;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace OpenTelemetry.Exporter.Geneva.Tests;

public class UnixDomainSocketEndPointTests
{
    [Fact]
    public void UnixDomainSocketEndPoint_constructor_InvalidArgument()
    {
        Assert.Throws<ArgumentNullException>(() => _ = new UnixDomainSocketEndPoint(null));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new UnixDomainSocketEndPoint(string.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new UnixDomainSocketEndPoint(new string('a', 100)));
    }

    [Fact]
    public void UnixDomainSocketEndPoint_constructor_Success()
    {
        var endpoint = new UnixDomainSocketEndPoint("abc");
        Assert.Equal("abc", endpoint.ToString());
    }

    [Fact]
    public void UnixDomainSocketEndPoint_Create_InvalidArgument()
    {
        var endpoint = new UnixDomainSocketEndPoint("abc");
        Assert.Throws<ArgumentNullException>(() => _ = endpoint.Create(null));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = endpoint.Create(this.CreateSocketAddress(new string('a', 100))));
    }

    [Fact]
    public void UnixDomainSocketEndPoint_Create_Success()
    {
        var endpoint = new UnixDomainSocketEndPoint("abc");

        var sa = new SocketAddress(AddressFamily.Unix, 2);  // SocketAddress size is 2
        Assert.Equal(string.Empty, endpoint.Create(sa).ToString());

        Assert.Equal("\0", endpoint.Create(this.CreateSocketAddress(string.Empty)).ToString());
        Assert.Equal("test\0", endpoint.Create(this.CreateSocketAddress("test")).ToString());
    }

    [Fact]
    public void UnixDomainSocketEndPoint_Serialize()
    {
        var path = "abc";
        var endpoint = new UnixDomainSocketEndPoint(path);
        Assert.Equal(this.CreateSocketAddress(path), endpoint.Serialize());
    }

    private SocketAddress CreateSocketAddress(string path)
    {
        int NativePathOffset = 2;
        var nativePath = Encoding.UTF8.GetBytes(path);
        var sa = new SocketAddress(AddressFamily.Unix, NativePathOffset + nativePath.Length + 1);
        for (int i = 0; i < nativePath.Length; ++i)
        {
            sa[NativePathOffset + i] = nativePath[i];
        }

        sa[NativePathOffset + nativePath.Length] = 0;
        return sa;
    }
}
