using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OpenTelemetry.Exporter.Geneva
{
    internal class UnixDomainSocketEndPoint : EndPoint
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
            this.path = path ?? throw new ArgumentNullException(nameof(path), "Path cannot be null.");
            nativePath = Encoding.UTF8.GetBytes(path);
            if (nativePath.Length == 0 || nativePath.Length > MaximumNativePathLength)
            {
                throw new ArgumentOutOfRangeException(nameof(nativePath), "Path is of an invalid length for use with domain sockets.");
            }
        }

        public override AddressFamily AddressFamily => AddressFamily.Unix;

        public override EndPoint Create(SocketAddress socketAddress) => new UnixDomainSocketEndPoint(socketAddress);

        private UnixDomainSocketEndPoint(SocketAddress socketAddress)
        {
            if (socketAddress == null)
            {
                throw new ArgumentNullException(nameof(socketAddress), "SocketAddress cannot be null.");
            }

            if (socketAddress.Family != AddressFamily ||
                socketAddress.Size > NativePathOffset + MaximumNativePathLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(socketAddress),
                    "The path of SocketAddress is of an invalid length for use with domain sockets.");
            }

            if (socketAddress.Size > NativePathOffset)
            {
                nativePath = new byte[socketAddress.Size - NativePathOffset];
                for (int i = 0; i < nativePath.Length; ++i)
                {
                    nativePath[i] = socketAddress[NativePathOffset + i];
                }

                path = Encoding.UTF8.GetString(nativePath);
            }
            else
            {
                path = string.Empty;
                nativePath = Array.Empty<byte>();
            }
        }

        public override SocketAddress Serialize()
        {
            var socketAddress = new SocketAddress(AddressFamily.Unix, NativePathOffset + nativePath.Length + 1);
            for (int i = 0; i < nativePath.Length; ++i)
            {
                socketAddress[NativePathOffset + i] = nativePath[i];
            }

            socketAddress[NativePathOffset + nativePath.Length] = 0;  // SocketAddress should be NULL terminated
            return socketAddress;
        }

        public override string ToString() => path;
    }
}
