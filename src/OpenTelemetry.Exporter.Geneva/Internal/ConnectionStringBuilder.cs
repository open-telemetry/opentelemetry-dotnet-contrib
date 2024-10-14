// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using OpenTelemetry.Exporter.Geneva.Transports;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Geneva;

internal enum TransportProtocol
{
    Etw,
    Tcp,
    Udp,
    Unix,
    EtwTld,
    Unspecified,
}

internal sealed class ConnectionStringBuilder
{
    private readonly Dictionary<string, string> parts = new(StringComparer.Ordinal);

    public ConnectionStringBuilder([NotNull] string? connectionString)
    {
        Guard.ThrowIfNullOrWhitespace(connectionString);

        const char Semicolon = ';';
        const char EqualSign = '=';
        foreach (var token in connectionString.Split(Semicolon))
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

#if NET
            var index = token.IndexOf(EqualSign, StringComparison.Ordinal);
#else
            var index = token.IndexOf(EqualSign);
#endif
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

            this.parts[key] = value;
        }

        if (this.parts.Count == 0)
        {
            throw new ArgumentNullException(nameof(connectionString), $"{nameof(connectionString)} is invalid.");
        }
    }

    public string EtwSession
    {
        get => this.ThrowIfNotExists<string>(nameof(this.EtwSession));
        set => this.parts[nameof(this.EtwSession)] = value;
    }

    public bool PrivatePreviewEnableTraceLoggingDynamic
    {
        get
        {
            return this.parts.TryGetValue(nameof(this.PrivatePreviewEnableTraceLoggingDynamic), out var value)
                && bool.TrueString.Equals(value, StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool PrivatePreviewEnableOtlpProtobufEncoding
    {
        get
        {
            return this.parts.TryGetValue(nameof(this.PrivatePreviewEnableOtlpProtobufEncoding), out var value)
                && bool.TrueString.Equals(value, StringComparison.OrdinalIgnoreCase);
        }
    }

    public string Endpoint
    {
        get => this.ThrowIfNotExists<string>(nameof(this.Endpoint));
        set => this.parts[nameof(this.Endpoint)] = value;
    }

    public TransportProtocol Protocol
    {
        get
        {
            try
            {
                // Checking Etw first, since it's preferred for Windows and enables fail fast on Linux
                if (this.parts.ContainsKey(nameof(this.EtwSession)))
                {
                    if (this.PrivatePreviewEnableTraceLoggingDynamic)
                    {
                        return TransportProtocol.EtwTld;
                    }

                    return TransportProtocol.Etw;
                }

                if (!this.parts.ContainsKey(nameof(this.Endpoint)))
                {
                    return TransportProtocol.Unspecified;
                }

                var endpoint = new Uri(this.Endpoint);
                if (Enum.TryParse(endpoint.Scheme, true, out TransportProtocol protocol))
                {
                    return protocol;
                }

                throw new ArgumentException("Endpoint scheme is invalid.");
            }
            catch (UriFormatException ex)
            {
                throw new ArgumentException($"{nameof(this.Endpoint)} value is malformed.", ex);
            }
        }
    }

    public int TimeoutMilliseconds
    {
        get
        {
            if (!this.parts.TryGetValue(nameof(this.TimeoutMilliseconds), out string? value))
            {
                return UnixDomainSocketDataTransport.DefaultTimeoutMilliseconds;
            }

            try
            {
                int timeout = int.Parse(value, CultureInfo.InvariantCulture);
                if (timeout <= 0)
                {
                    throw new ArgumentException(
                        $"{nameof(this.TimeoutMilliseconds)} should be greater than zero.",
                        nameof(this.TimeoutMilliseconds));
                }

                return timeout;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"{nameof(this.TimeoutMilliseconds)} is malformed.",
                    nameof(this.TimeoutMilliseconds),
                    ex);
            }
        }
        set => this.parts[nameof(this.TimeoutMilliseconds)] = value.ToString(CultureInfo.InvariantCulture);
    }

    public string Host
    {
        get
        {
            try
            {
                var endpoint = new Uri(this.Endpoint);
                return endpoint.Host;
            }
            catch (UriFormatException ex)
            {
                throw new ArgumentException($"{nameof(this.Endpoint)} value is malformed.", ex);
            }
        }
    }

    public int Port
    {
        get
        {
            try
            {
                var endpoint = new Uri(this.Endpoint);
                if (endpoint.IsDefaultPort)
                {
                    throw new ArgumentException($"Port should be explicitly set in {nameof(this.Endpoint)} value.");
                }

                return endpoint.Port;
            }
            catch (UriFormatException ex)
            {
                throw new ArgumentException($"{nameof(this.Endpoint)} value is malformed.", ex);
            }
        }
    }

    public string Account
    {
        get => this.ThrowIfNotExists<string>(nameof(this.Account));
        set => this.parts[nameof(this.Account)] = value;
    }

    public string Namespace
    {
        get => this.ThrowIfNotExists<string>(nameof(this.Namespace));
        set => this.parts[nameof(this.Namespace)] = value;
    }

    public bool DisableMetricNameValidation
    {
        get
        {
            if (!this.parts.TryGetValue(nameof(this.DisableMetricNameValidation), out var value))
            {
                return false;
            }

            return string.Equals(bool.TrueString, value, StringComparison.OrdinalIgnoreCase);
        }
        set => this.parts[nameof(this.DisableMetricNameValidation)] = value ? bool.TrueString : bool.FalseString;
    }

    public string ParseUnixDomainSocketPath()
    {
        try
        {
            var endpoint = new Uri(this.Endpoint);
            return ReplaceFirstChar(endpoint.AbsolutePath, '@', '\0');
        }
        catch (UriFormatException ex)
        {
            throw new ArgumentException($"{nameof(this.Endpoint)} value is malformed.", ex);
        }
    }

    /// <summary>
    /// Replace first charater of string if it matches with <paramref name="oldChar"/> with <paramref name="newChar"/>.
    /// </summary>
    /// <param name="str">String to be updated.</param>
    /// <param name="oldChar">Old character to be replaced.</param>
    /// <param name="newChar">New character to be replaced with.</param>
    /// <returns>Updated string.</returns>
    internal static string ReplaceFirstChar(string str, char oldChar, char newChar)
    {
        if (str.Length > 0 && str[0] == oldChar)
        {
            return $"{newChar}{str.Substring(1)}";
        }

        return str;
    }

    private T ThrowIfNotExists<T>(string name)
    {
        if (!this.parts.TryGetValue(name, out var value))
        {
            throw new ArgumentException($"'{name}' value is missing in connection string.");
        }

        return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }
}
