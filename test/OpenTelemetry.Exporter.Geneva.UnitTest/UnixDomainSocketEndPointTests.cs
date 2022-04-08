using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace OpenTelemetry.Exporter.Geneva.UnitTest
{
    public class UnixDomainSocketEndPointTests
    {
        [Fact]
        public void UnixDomainSocketEndPoint_constructor_InvalidArgument()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new UnixDomainSocketEndPoint(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new UnixDomainSocketEndPoint(""));
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
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = endpoint.Create(CreateSocketAddress(new string('a', 100))));
        }

        [Fact]
        public void UnixDomainSocketEndPoint_Create_Success()
        {
            var endpoint = new UnixDomainSocketEndPoint("abc");

            var sa = new SocketAddress(AddressFamily.Unix, 2);  // SocketAddress size is 2
            Assert.Equal("", endpoint.Create(sa).ToString());

            Assert.Equal("\0", endpoint.Create(CreateSocketAddress("")).ToString());
            Assert.Equal("test\0", endpoint.Create(CreateSocketAddress("test")).ToString());
        }

        [Fact]
        public void UnixDomainSocketEndPoint_Serialize()
        {
            var path = "abc";
            var endpoint = new UnixDomainSocketEndPoint(path);
            Assert.Equal(CreateSocketAddress(path), endpoint.Serialize());
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
}
