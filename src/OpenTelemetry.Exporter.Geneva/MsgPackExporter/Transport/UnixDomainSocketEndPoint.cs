// <copyright file="UnixDomainSocketEndPoint.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Geneva;

internal sealed class UnixDomainSocketEndPoint : EndPoint
{
    // sockaddr_un.sun_path at http://pubs.opengroup.org/onlinepubs/9699919799/basedefs/sys_un.h.html, -1 for terminator
    private const int MaximumNativePathLength = 92 - 1;

    // The first 2 bytes of the underlying buffer are reserved for the AddressFamily enumerated value.
    // https://docs.microsoft.com/dotnet/api/system.net.socketaddress
    private const int NativePathOffset = 2;
    private readonly string path;
    private readonly byte[] nativePath;

    public UnixDomainSocketEndPoint(string path)
    {
        Guard.ThrowIfNull(path);

        this.path = path;
        this.nativePath = Encoding.UTF8.GetBytes(path);
        if (this.nativePath.Length == 0 || this.nativePath.Length > MaximumNativePathLength)
        {
            throw new ArgumentOutOfRangeException(nameof(this.nativePath), "Path is of an invalid length for use with domain sockets.");
        }
    }

    public override AddressFamily AddressFamily => AddressFamily.Unix;

    public override EndPoint Create(SocketAddress socketAddress) => new UnixDomainSocketEndPoint(socketAddress);

    private UnixDomainSocketEndPoint(SocketAddress socketAddress)
    {
        Guard.ThrowIfNull(socketAddress);

        if (socketAddress.Family != this.AddressFamily ||
            socketAddress.Size > NativePathOffset + MaximumNativePathLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(socketAddress),
                "The path of SocketAddress is of an invalid length for use with domain sockets.");
        }

        if (socketAddress.Size > NativePathOffset)
        {
            this.nativePath = new byte[socketAddress.Size - NativePathOffset];
            for (int i = 0; i < this.nativePath.Length; ++i)
            {
                this.nativePath[i] = socketAddress[NativePathOffset + i];
            }

            this.path = Encoding.UTF8.GetString(this.nativePath);
        }
        else
        {
            this.path = string.Empty;
            this.nativePath = Array.Empty<byte>();
        }
    }

    public override SocketAddress Serialize()
    {
        var socketAddress = new SocketAddress(AddressFamily.Unix, NativePathOffset + this.nativePath.Length + 1);
        for (int i = 0; i < this.nativePath.Length; ++i)
        {
            socketAddress[NativePathOffset + i] = this.nativePath[i];
        }

        socketAddress[NativePathOffset + this.nativePath.Length] = 0;  // SocketAddress should be NULL terminated
        return socketAddress;
    }

    public override string ToString() => this.path;
}
