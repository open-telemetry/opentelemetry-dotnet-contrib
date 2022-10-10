// <copyright file="LogSerializationTests.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Exporter.Geneva.Tests;

public class LogSerializationTests
{
    /*
    Run from the current directory:
    dotnet test -f net6.0 --filter FullyQualifiedName~LogSerializationTests -l "console;verbosity=detailed"
    */
    private readonly ITestOutputHelper output;

    public LogSerializationTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void SerializationTestForException()
    {
        var exceptionMessage = "Exception Message";
        var ex = new Exception(exceptionMessage);
        var exportedFields = GetExportedFieldsAfterLogging(logger => logger.Log<object>(
            logLevel: LogLevel.Information,
            eventId: default,
            state: null,
            exception: ex,
            formatter: null),
            (genevaOptions) => genevaOptions.ExceptionStackExportOption = ExceptionStackExportOptions.ExportAsString);

        var actualExceptionMessage = exportedFields["env_ex_msg"];
        Assert.Equal(exceptionMessage, actualExceptionMessage);

        var actualExceptionType = exportedFields["env_ex_type"];
        Assert.Equal("System.Exception", actualExceptionType);

        var actualExceptionStack = exportedFields["env_ex_stack"];
        Assert.Equal(ex.ToString(), actualExceptionStack);
    }

    private static void PrintFields(ITestOutputHelper output, Dictionary<object, object> fields)
    {
        foreach (var field in fields)
        {
            output.WriteLine($"{field.Key}:{field.Value}");
        }
    }

    private static Dictionary<object, object> GetExportedFieldsAfterLogging(Action<ILogger> doLog, Action<GenevaExporterOptions> configureGeneva = null)
    {
        Socket server = null;
        string path = string.Empty;
        try
        {
            var logRecordList = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder => builder
                    .AddOpenTelemetry(options =>
                    {
                        options.AddInMemoryExporter(logRecordList);
                    })
                    .AddFilter(typeof(LogSerializationTests).FullName, LogLevel.Trace)); // Enable all LogLevels

            var logger = loggerFactory.CreateLogger<LogSerializationTests>();
            doLog(logger);

            Assert.Single(logRecordList);
            var exporterOptions = new GenevaExporterOptions();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                exporterOptions.ConnectionString = "EtwSession=OpenTelemetry";
            }
            else
            {
                path = GenerateTempFilePath();
                exporterOptions.ConnectionString = "Endpoint=unix:" + path;
                var endpoint = new UnixDomainSocketEndPoint(path);
                server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                server.Bind(endpoint);
                server.Listen(1);
            }

            configureGeneva?.Invoke(exporterOptions);

            using var exporter = new MsgPackLogExporter(exporterOptions);
            var m_buffer = typeof(MsgPackLogExporter).GetField("m_buffer", BindingFlags.NonPublic | BindingFlags.Static).GetValue(exporter) as ThreadLocal<byte[]>;
            _ = exporter.SerializeLogRecord(logRecordList[0]);
            object fluentdData = MessagePack.MessagePackSerializer.Deserialize<object>(m_buffer.Value, MessagePack.Resolvers.ContractlessStandardResolver.Instance);

            return GetFields(fluentdData);
        }
        finally
        {
            server?.Dispose();
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }
    }

    private static string GenerateTempFilePath()
    {
        while (true)
        {
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            if (!File.Exists(path))
            {
                return path;
            }
        }
    }

    private static Dictionary<object, object> GetFields(object fluentdData)
    {
        /* Fluentd Forward Mode:
        [
            "Log",
            [
                [ <timestamp>, { "env_ver": "4.0", ... } ]
            ],
            { "TimeFormat": "DateTime" }
        ]
        */

        var TimeStampAndMappings = ((fluentdData as object[])[1] as object[])[0];
        var mapping = (TimeStampAndMappings as object[])[1] as Dictionary<object, object>;
        return mapping;
    }
}
