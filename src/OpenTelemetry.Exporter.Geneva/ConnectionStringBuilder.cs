using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenTelemetry.Exporter.Geneva
{
    internal enum TransportProtocol
    {
        Etw,
        Tcp,
        Udp,
        Unix,
        Unspecified
    }

    internal class ConnectionStringBuilder
    {
        private readonly Dictionary<string, string> _parts = new Dictionary<string, string>(StringComparer.Ordinal);

        public ConnectionStringBuilder(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString), $"{nameof(connectionString)} is invalid.");
            }

            const char Semicolon = ';';
            const char EqualSign = '=';
            foreach (var token in connectionString.Split(Semicolon))
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                var index = token.IndexOf(EqualSign);
                if (index == -1 || index != token.LastIndexOf(EqualSign))
                {
                    continue;
                }

                var pair = token.Trim().Split(EqualSign);

                var key = pair[0].Trim();
                var value = pair[1].Trim();
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException("Connection string cannot contain empty keys or values.");
                }

                _parts[key] = value;
            }

            if (_parts.Count == 0)
            {
                throw new ArgumentNullException(nameof(connectionString), $"{nameof(connectionString)} is invalid.");
            }
        }

        public string EtwSession
        {
            get => ThrowIfNotExists<string>(nameof(EtwSession));
            set => _parts[nameof(EtwSession)] = value;
        }

        public string Endpoint
        {
            get => ThrowIfNotExists<string>(nameof(Endpoint));
            set => _parts[nameof(Endpoint)] = value;
        }

        public TransportProtocol Protocol
        {
            get
            {
                try
                {
                    // Checking Etw first, since it's preferred for Windows and enables fail fast on Linux
                    if (_parts.ContainsKey(nameof(EtwSession)))
                    {
                        return TransportProtocol.Etw;
                    }

                    if (!_parts.ContainsKey(nameof(Endpoint)))
                    {
                        return TransportProtocol.Unspecified;
                    }

                    var endpoint = new Uri(Endpoint);
                    if (Enum.TryParse(endpoint.Scheme, true, out TransportProtocol protocol))
                    {
                        return protocol;
                    }

                    throw new ArgumentException("Endpoint scheme is invalid.");
                }
                catch (UriFormatException ex)
                {
                    throw new ArgumentException($"{nameof(Endpoint)} value is malformed.", ex);
                }
            }
        }

        public string ParseUnixDomainSocketPath()
        {
            try
            {
                var endpoint = new Uri(Endpoint);
                return endpoint.AbsolutePath;
            }
            catch (UriFormatException ex)
            {
                throw new ArgumentException($"{nameof(Endpoint)} value is malformed.", ex);
            }
        }

        public int TimeoutMilliseconds
        {
            get
            {
                if (!_parts.TryGetValue(nameof(TimeoutMilliseconds), out string value))
                {
                    return UnixDomainSocketDataTransport.DefaultTimeoutMilliseconds;
                }

                try
                {
                    int timeout = int.Parse(value, CultureInfo.InvariantCulture);
                    if (timeout <= 0)
                    {
                        throw new ArgumentException($"{nameof(TimeoutMilliseconds)} should be greater than zero.",
                            nameof(TimeoutMilliseconds));
                    }

                    return timeout;
                }
                catch (ArgumentException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"{nameof(TimeoutMilliseconds)} is malformed.",
                        nameof(TimeoutMilliseconds), ex);
                }
            }
            set => _parts[nameof(TimeoutMilliseconds)] = value.ToString(CultureInfo.InvariantCulture);
        }

        public string Host
        {
            get
            {
                try
                {
                    var endpoint = new Uri(Endpoint);
                    return endpoint.Host;
                }
                catch (UriFormatException ex)
                {
                    throw new ArgumentException($"{nameof(Endpoint)} value is malformed.", ex);
                }
            }
        }

        public int Port
        {
            get
            {
                try
                {
                    var endpoint = new Uri(Endpoint);
                    if (endpoint.IsDefaultPort)
                    {
                        throw new ArgumentException($"Port should be explicitly set in {nameof(Endpoint)} value.");
                    }

                    return endpoint.Port;
                }
                catch (UriFormatException ex)
                {
                    throw new ArgumentException($"{nameof(Endpoint)} value is malformed.", ex);
                }
            }
        }

        public string Account
        {
            get => ThrowIfNotExists<string>(nameof(Account));
            set => _parts[nameof(Account)] = value;
        }

        public string Namespace
        {
            get => ThrowIfNotExists<string>(nameof(Namespace));
            set => _parts[nameof(Namespace)] = value;
        }

        private T ThrowIfNotExists<T>(string name)
        {
            if (!_parts.TryGetValue(name, out var value))
            {
                throw new ArgumentException($"'{name}' value is missing in connection string.");
            }

            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }
    }
}
