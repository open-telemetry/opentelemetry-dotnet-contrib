using System;
using System.Net;
using System.Net.Sockets;

namespace OpenTelemetry.Exporter.Geneva
{
    internal class UnixDomainSocketDataTransport : IDataTransport, IDisposable
    {
        public const int DefaultTimeoutMilliseconds = 15000;
        private readonly EndPoint unixEndpoint;
        private Socket socket;
        private int TimeoutMilliseconds;

        /// <summary>
        /// The class for transporting data over Unix domain socket.
        /// </summary>
        /// <param name="unixDomainSocketPath">The path to connect a unix domain socket over.</param>
        /// <param name="timeoutMilliseconds">
        /// The time-out value, in milliseconds.
        /// If you set the property with a value between 1 and 499, the value will be changed to 500.
        /// The default value is 15,000 milliseconds.
        /// </param>
        public UnixDomainSocketDataTransport(string unixDomainSocketPath,
            int timeoutMilliseconds = DefaultTimeoutMilliseconds)
        {
            unixEndpoint = new UnixDomainSocketEndPoint(unixDomainSocketPath);
            this.TimeoutMilliseconds = timeoutMilliseconds;
            Connect();
        }

        public bool IsEnabled()
        {
            return true;
        }

        public void Send(byte[] data, int size)
        {
            try
            {
                if (!socket.Connected)
                {
                    // Socket connection is off! Server might have stopped. Trying to reconnect.
                    Reconnect();
                }
                socket.Send(data, size, SocketFlags.None);
            }
            catch (SocketException ex)
            {
                // SocketException from Socket.Send
                ExporterEventSource.Log.ExporterException(ex);
            }
            catch (Exception ex)
            {
                ExporterEventSource.Log.ExporterException(ex);
            }
        }

        public void Dispose()
        {
            socket.Dispose();
        }

        private void Connect()
        {
            try
            {
                socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP)
                {
                    SendTimeout = this.TimeoutMilliseconds
                };
                socket.Connect(unixEndpoint);
            }
            catch (Exception ex)
            {
                ExporterEventSource.Log.ExporterException(ex);
                // Re-throw the exception to
                // 1. fail fast in Geneva exporter contructor, or
                // 2. fail in the Reconnect attempt.
                throw;
            }
        }

        private void Reconnect()
        {
            socket.Close();
            Connect();
        }
    }
}
