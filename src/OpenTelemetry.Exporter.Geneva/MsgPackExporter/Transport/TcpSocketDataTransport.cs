using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

// Do not create tasks without passing a TaskScheduler (it was already doing it before) (also it complains even with a scheduler)
#pragma warning disable CA2008
namespace OpenTelemetry.Exporter.Geneva;

internal class TcpSocketDataTransport : IDataTransport, IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly object _socketCreationLock = new object();
    private readonly Action _onTcpConnectionSuccess;
    private readonly Action<Exception> _onTcpConnectionFailure;

    private Socket socket;
    private Task connectTask;

    public TcpSocketDataTransport(string host, int port, Action onTcpConnectionSuccess, Action<Exception> onTcpConnectionFailure)
    {
        this._host = host;
        this._port = port;

        this._onTcpConnectionSuccess = onTcpConnectionSuccess;
        this._onTcpConnectionFailure = onTcpConnectionFailure;

        try
        {
            this.Connect();
        }
        catch (SocketException)
        {
            this.ReconnectInBackground();
        }
    }

    public bool IsEnabled()
    {
        return true;
    }

    public void Send(byte[] data, int size)
    {
        try
        {
            if (this.socket.Connected)
            {
                this.SendAsync(data, size);
            }
            else
            {
                this.ReconnectInBackground();
            }
        }
        catch (SocketException ex)
        {
            // SocketException from Socket.BeginSend inside SendAsync
            ExporterEventSource.Log.ExporterException("ExporterException", ex);
            this.ReconnectInBackground();
        }
        catch (Exception ex)
        {
            ExporterEventSource.Log.ExporterException("ExporterException", ex);
        }
    }

    public void Dispose()
    {
        this.socket.Dispose();
    }

    private void Connect()
    {
        try
        {
            lock (this._socketCreationLock)
            {
                if (this.socket != null && this.socket.Connected)
                {
                    return;
                }

                if (this.socket != null)
                {
                    this.socket.Dispose();
                }

                EndPoint tcpEndpoint;
                if (IPAddress.TryParse(this._host, out var ipAddress))
                {
                    tcpEndpoint = new IPEndPoint(ipAddress, this._port);
                }
                else
                {
                    tcpEndpoint = new DnsEndPoint(this._host, this._port);
                }

                this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);

                this.socket.Connect(tcpEndpoint);
            }

            this._onTcpConnectionSuccess?.Invoke();
        }
        catch (Exception ex)
        {
            ExporterEventSource.Log.ExporterException("ExporterException", ex);
            // Re-throw the exception to
            // 1. fail fast in Geneva exporter contructor, or
            // 2. fail the Connect task initialized for a Send failure.
            this._onTcpConnectionFailure?.Invoke(ex);
            throw;
        }
    }

    private void ReconnectInBackground()
    {
        if (this.connectTask == null || this.connectTask.IsCompleted)
        {
            this.connectTask = Task.Run(this.Connect);

            // exception is captured in the connect method, don't recapture.
            // retry the connection after a wait (but dont block the main thread).
            this.connectTask.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Task.Run(async () =>
                    {
                        await Task.Delay(3000).ConfigureAwait(false);
                        this.ReconnectInBackground();
                    });
                }
            });
        }
    }

    private void SendAsync(byte[] data, int size)
    {
        // Begin sending the data to the remote device.
        this.socket.BeginSend(data, 0, size, SocketFlags.None, new AsyncCallback(this.SendCallback), this.socket);
    }

    private void SendCallback(IAsyncResult ar)
    {
        try
        {
            // Retrieve the socket from the state object.
            Socket client = (Socket)ar.AsyncState;

            // Complete sending the data to the remote device.
            int bytesSent = client.EndSend(ar);
        }
        catch (SocketException ex)
        {
            // SocketException from Socket.EndSend
            ExporterEventSource.Log.ExporterException("ExporterException", ex);
            this.ReconnectInBackground();
        }
        catch (Exception ex)
        {
            ExporterEventSource.Log.ExporterException("ExporterException", ex);
        }
    }
}
#pragma warning restore CA2008 // Do not create tasks without passing a TaskScheduler
