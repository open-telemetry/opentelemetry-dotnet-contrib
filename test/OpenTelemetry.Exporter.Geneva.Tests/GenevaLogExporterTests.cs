// <copyright file="GenevaLogExporterTests.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using Xunit;

namespace OpenTelemetry.Exporter.Geneva.Tests
{
    public class GenevaLogExporterTests
    {
        [Fact]
        public void BadArgs()
        {
            GenevaExporterOptions exporterOptions = null;
            Assert.Throws<ArgumentNullException>(() =>
            {
                using var exporter = new GenevaLogExporter(exporterOptions);
            });
        }

        [Fact]
        public void SpecialChractersInTableNameMappings()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                using var exporter = new GenevaLogExporter(new GenevaExporterOptions
                {
                    TableNameMappings = new Dictionary<string, string> { ["TestCategory"] = "\u0418" },
                });
            });

            Assert.Throws<ArgumentException>(() =>
            {
                using var exporter = new GenevaLogExporter(new GenevaExporterOptions
                {
                    TableNameMappings = new Dictionary<string, string> { ["*"] = "\u0418" },
                });
            });

            // Throw on null value - include key in exception message
            var ex = Assert.Throws<ArgumentNullException>(() =>
            {
                new GenevaExporterOptions
                {
                    TableNameMappings = new Dictionary<string, string> { ["TestCategory"] = null },
                };
            });
            Assert.Contains("TableNameMappings must not contain null values.", ex.Message);
            Assert.Equal("TestCategory", ex.ParamName);

            // Throw when TableNameMappings is null
            Assert.Throws<ArgumentNullException>(() =>
            {
                new GenevaExporterOptions
                {
                    TableNameMappings = null,
                };
            });
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void InvalidConnectionString(string connectionString)
        {
            var exporterOptions = new GenevaExporterOptions() { ConnectionString = connectionString };
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                using var exporter = new GenevaLogExporter(exporterOptions);
            });
        }

        [Fact]
        public void IncompatibleConnectionString_Windows()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var exporterOptions = new GenevaExporterOptions() { ConnectionString = "Endpoint=unix:" + @"C:\Users\user\AppData\Local\Temp\14tj4ac4.v2q" };
                var exception = Assert.Throws<ArgumentException>(() =>
                {
                    using var exporter = new GenevaLogExporter(exporterOptions);
                });
                Assert.Equal("Unix domain socket should not be used on Windows.", exception.Message);
            }
        }

        [Fact]
        public void IncompatibleConnectionString_Linux()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var exporterOptions = new GenevaExporterOptions() { ConnectionString = "EtwSession=OpenTelemetry" };
                var exception = Assert.Throws<ArgumentException>(() =>
                {
                    using var exporter = new GenevaLogExporter(exporterOptions);
                });
                Assert.Equal("ETW cannot be used on non-Windows operating systems.", exception.Message);
            }
        }

        [Theory]
        [InlineData("categoryA", "TableA")]
        [InlineData("categoryB", "TableB")]
        [InlineData("categoryA", "TableA", "categoryB", "TableB")]
        [InlineData("categoryA", "TableA", "*", "CatchAll")]
        [InlineData(null)]
        public void TableNameMappingTest(params string[] category)
        {
            // ARRANGE
            string path = string.Empty;
            Socket server = null;
            var logRecordList = new List<LogRecord>();
            Dictionary<string, string> mappingsDict = null;
            try
            {
                var exporterOptions = new GenevaExporterOptions();
                if (category?.Length > 0)
                {
                    mappingsDict = new Dictionary<string, string>();
                    for (int i = 0; i < category.Length; i = i + 2)
                    {
                        mappingsDict.Add(category[i], category[i + 1]);
                    }

                    exporterOptions.TableNameMappings = mappingsDict;
                }

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

                using var loggerFactory = LoggerFactory.Create(builder => builder
                .AddOpenTelemetry(options =>
                {
                    options.AddInMemoryExporter(logRecordList);
                })
                .AddFilter("*", LogLevel.Trace)); // Enable all LogLevels

                // Create a test exporter to get MessagePack byte data to validate if the data was serialized correctly.
                using var exporter = new GenevaLogExporter(exporterOptions);

                ILogger logger;
                ThreadLocal<byte[]> m_buffer;
                object fluentdData;
                string actualTableName;
                string defaultLogTable = "Log";
                if (mappingsDict != null)
                {
                    foreach (var mapping in mappingsDict)
                    {
                        if (!mapping.Key.Equals("*"))
                        {
                            logger = loggerFactory.CreateLogger(mapping.Key);
                            logger.LogError("this does not matter");

                            Assert.Single(logRecordList);
                            m_buffer = typeof(GenevaLogExporter).GetField("m_buffer", BindingFlags.NonPublic | BindingFlags.Static).GetValue(exporter) as ThreadLocal<byte[]>;
                            _ = exporter.SerializeLogRecord(logRecordList[0]);
                            fluentdData = MessagePack.MessagePackSerializer.Deserialize<object>(m_buffer.Value, MessagePack.Resolvers.ContractlessStandardResolver.Instance);
                            actualTableName = (fluentdData as object[])[0] as string;
                            Assert.Equal(mapping.Value, actualTableName);
                            logRecordList.Clear();
                        }
                        else
                        {
                            defaultLogTable = mapping.Value;
                        }
                    }

                    // test default table
                    logger = loggerFactory.CreateLogger("random category");
                    logger.LogError("this does not matter");

                    Assert.Single(logRecordList);
                    m_buffer = typeof(GenevaLogExporter).GetField("m_buffer", BindingFlags.NonPublic | BindingFlags.Static).GetValue(exporter) as ThreadLocal<byte[]>;
                    _ = exporter.SerializeLogRecord(logRecordList[0]);
                    fluentdData = MessagePack.MessagePackSerializer.Deserialize<object>(m_buffer.Value, MessagePack.Resolvers.ContractlessStandardResolver.Instance);
                    actualTableName = (fluentdData as object[])[0] as string;
                    Assert.Equal(defaultLogTable, actualTableName);
                    logRecordList.Clear();
                }
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

        [Fact]
        public void PassThruTableMappingsWhenTheRuleIsEnabled()
        {
            string path = string.Empty;
            Socket server = null;
            try
            {
                var userInitializedCategoryToTableNameMappings = new Dictionary<string, string>
                {
                    ["Company.Store"] = "Store",
                    ["Company.Orders"] = "Orders",
                    ["*"] = "*",
                };

                var expectedCategoryToTableNameList = new List<KeyValuePair<string, string>>
            {
                // The category name must match "^[A-Z][a-zA-Z0-9]*$"; any character that is not allowed will be removed.
                new KeyValuePair<string, string>("Company.Customer", "CompanyCustomer"),

                new KeyValuePair<string, string>("Company-%-Customer*Region$##", "CompanyCustomerRegion"),

                // If the first character in the resulting string is lower-case ALPHA,
                // it will be converted to the corresponding upper-case.
                new KeyValuePair<string, string>("company.Calendar", "CompanyCalendar"),

                // After removing not allowed characters,
                // if the resulting string is still an illegal event name, the data will get dropped on the floor.
                new KeyValuePair<string, string>("$&-.$~!!", null),

                new KeyValuePair<string, string>("dlmwl3bvd84bxsx8wf700nx9rydrrhfewbxf82ceoo0h8rpla4", "Dlmwl3bvd84bxsx8wf700nx9rydrrhfewbxf82ceoo0h8rpla4"),

                // If the resulting string is longer than 50 characters, only the first 50 characters will be taken.
                new KeyValuePair<string, string>("Company.Customer.rsLiheLClHJasBOvM.XI4uW7iop6ghvwBzahfs", "CompanyCustomerrsLiheLClHJasBOvMXI4uW7iop6ghvwBzah"),

                // The data will be dropped on the floor as the exporter cannot deduce a valid table name.
                new KeyValuePair<string, string>("1.2", null),
            };

                var logRecordList = new List<LogRecord>();
                var exporterOptions = new GenevaExporterOptions
                {
                    TableNameMappings = userInitializedCategoryToTableNameMappings,
                };

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

                using var loggerFactory = LoggerFactory.Create(builder => builder
                .AddOpenTelemetry(options =>
                {
                    options.AddInMemoryExporter(logRecordList);
                })
                .AddFilter("*", LogLevel.Trace)); // Enable all LogLevels

                // Create a test exporter to get MessagePack byte data to validate if the data was serialized correctly.
                using var exporter = new GenevaLogExporter(exporterOptions);

                ILogger passThruTableMappingsLogger, userInitializedTableMappingsLogger;
                ThreadLocal<byte[]> m_buffer;
                object fluentdData;
                string actualTableName;
                m_buffer = typeof(GenevaLogExporter).GetField("m_buffer", BindingFlags.NonPublic | BindingFlags.Static).GetValue(exporter) as ThreadLocal<byte[]>;

                // Verify that the category table mappings specified by the users in the Geneva Configuration are mapped correctly.
                foreach (var mapping in userInitializedCategoryToTableNameMappings)
                {
                    if (mapping.Key != "*")
                    {
                        userInitializedTableMappingsLogger = loggerFactory.CreateLogger(mapping.Key);
                        userInitializedTableMappingsLogger.LogInformation("This information does not matter.");
                        Assert.Single(logRecordList);

                        _ = exporter.SerializeLogRecord(logRecordList[0]);
                        fluentdData = MessagePack.MessagePackSerializer.Deserialize<object>(m_buffer.Value, MessagePack.Resolvers.ContractlessStandardResolver.Instance);
                        actualTableName = (fluentdData as object[])[0] as string;
                        userInitializedCategoryToTableNameMappings.TryGetValue(mapping.Key, out var expectedTableNme);
                        Assert.Equal(expectedTableNme, actualTableName);

                        logRecordList.Clear();
                    }
                }

                // Verify that when the "*" = "*" were enabled, the correct table names were being deduced following the set of rules.
                foreach (var mapping in expectedCategoryToTableNameList)
                {
                    passThruTableMappingsLogger = loggerFactory.CreateLogger(mapping.Key);
                    passThruTableMappingsLogger.LogInformation("This information does not matter.");
                    Assert.Single(logRecordList);

                    _ = exporter.SerializeLogRecord(logRecordList[0]);
                    fluentdData = MessagePack.MessagePackSerializer.Deserialize<object>(m_buffer.Value, MessagePack.Resolvers.ContractlessStandardResolver.Instance);
                    actualTableName = (fluentdData as object[])[0] as string;
                    string expectedTableName = string.Empty;
                    expectedTableName = mapping.Value;
                    Assert.Equal(expectedTableName, actualTableName);

                    logRecordList.Clear();
                }
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

        [Fact]
        public void SerializeILoggerScopes()
        {
            string path = string.Empty;
            Socket senderSocket = null;
            Socket receiverSocket = null;
            try
            {
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
                    senderSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                    senderSocket.Bind(endpoint);
                    senderSocket.Listen(1);
                }

                using var loggerFactory = LoggerFactory.Create(builder => builder
                .AddOpenTelemetry(options =>
                {
                    options.IncludeScopes = true;
                    options.AddGenevaLogExporter(options =>
                    {
                        options.ConnectionString = exporterOptions.ConnectionString;
                    });
                }));

                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    receiverSocket = senderSocket.Accept();
                    receiverSocket.ReceiveTimeout = 10000;
                }

                // Create a test exporter to get MessagePack byte data to validate if the data was serialized correctly.
                using var exporter = new GenevaLogExporter(exporterOptions);

                // Emit a LogRecord and grab a copy of internal buffer for validation.
                var logger = loggerFactory.CreateLogger<GenevaLogExporterTests>();

                using (logger.BeginScope("MyOuterScope"))
                using (logger.BeginScope("MyInnerScope"))
                using (logger.BeginScope("MyInnerInnerScope with {name} and {age} of custom", "John Doe", 35))
                using (logger.BeginScope(new List<KeyValuePair<string, object>> { new KeyValuePair<string, object>("MyKey", "MyValue") }))
                {
                    logger.LogInformation("Hello from {food} {price}.", "artichoke", 3.99);
                }

                byte[] serializedData;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var m_buffer = typeof(GenevaLogExporter).GetField("m_buffer", BindingFlags.NonPublic | BindingFlags.Static).GetValue(exporter) as ThreadLocal<byte[]>;
                    serializedData = m_buffer.Value;
                }
                else
                {
                    // Read the data sent via socket.
                    serializedData = new byte[65360];
                    _ = receiverSocket.Receive(serializedData);
                }

                object fluentdData = MessagePack.MessagePackSerializer.Deserialize<object>(serializedData, MessagePack.Resolvers.ContractlessStandardResolver.Instance);
                var signal = (fluentdData as object[])[0] as string;
                var TimeStampAndMappings = ((fluentdData as object[])[1] as object[])[0];
                var mapping = (TimeStampAndMappings as object[])[1] as Dictionary<object, object>;
                var serializedScopes = mapping["scopes"] as object[];

                Assert.Equal(4, serializedScopes.Length);

                // Test 1st scope
                var scope = serializedScopes[0] as ICollection<KeyValuePair<object, object>>;
                Assert.Single(scope);
                Assert.Contains(new KeyValuePair<object, object>("scope", "MyOuterScope"), scope);

                // Test 2nd scope
                scope = serializedScopes[1] as ICollection<KeyValuePair<object, object>>;
                Assert.Single(scope);
                Assert.Contains(new KeyValuePair<object, object>("scope", "MyInnerScope"), scope);

                // Test 3rd scope
                scope = serializedScopes[2] as ICollection<KeyValuePair<object, object>>;
                Assert.Equal(3, scope.Count);
                Assert.Contains(new KeyValuePair<object, object>("name", "John Doe"), scope);
                Assert.Contains(new KeyValuePair<object, object>("age", (byte)35), scope);
                Assert.Contains(new KeyValuePair<object, object>("{OriginalFormat}", "MyInnerInnerScope with {name} and {age} of custom"), scope);

                // Test 4th scope
                scope = serializedScopes[3] as ICollection<KeyValuePair<object, object>>;
                Assert.Single(scope);
                Assert.Contains(new KeyValuePair<object, object>("MyKey", "MyValue"), scope);
            }
            finally
            {
                senderSocket?.Dispose();
                receiverSocket?.Dispose();
                try
                {
                    File.Delete(path);
                }
                catch
                {
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SerializationTestWithILoggerLogMethod(bool includeFormattedMessage)
        {
            // Dedicated test for the raw ILogger.Log method
            // https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger.log

            // ARRANGE
            string path = string.Empty;
            Socket server = null;
            var logRecordList = new List<LogRecord>();
            try
            {
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

                using var loggerFactory = LoggerFactory.Create(builder => builder
                .AddOpenTelemetry(options =>
                {
                    options.AddGenevaLogExporter(options =>
                    {
                        options.ConnectionString = exporterOptions.ConnectionString;
                    });
                    options.AddInMemoryExporter(logRecordList);
                    options.IncludeFormattedMessage = includeFormattedMessage;
                })
                .AddFilter(typeof(GenevaLogExporterTests).FullName, LogLevel.Trace)); // Enable all LogLevels

                // Create a test exporter to get MessagePack byte data to validate if the data was serialized correctly.
                using var exporter = new GenevaLogExporter(exporterOptions);

                // Emit a LogRecord and grab a copy of the LogRecord from the collection passed to InMemoryExporter
                var logger = loggerFactory.CreateLogger<GenevaLogExporterTests>();

                // ACT
                // This is treated as structured logging as the state can be converted to IReadOnlyList<KeyValuePair<string, object>>
                logger.Log(
                    LogLevel.Information,
                    default,
                    new List<KeyValuePair<string, object>>()
                    {
                        new KeyValuePair<string, object>("Key1", "Value1"),
                        new KeyValuePair<string, object>("Key2", "Value2"),
                    },
                    null,
                    (state, ex) => "Formatted Message");

                // VALIDATE
                Assert.Single(logRecordList);
                var m_buffer = typeof(GenevaLogExporter).GetField("m_buffer", BindingFlags.NonPublic | BindingFlags.Static).GetValue(exporter) as ThreadLocal<byte[]>;
                _ = exporter.SerializeLogRecord(logRecordList[0]);
                object fluentdData = MessagePack.MessagePackSerializer.Deserialize<object>(m_buffer.Value, MessagePack.Resolvers.ContractlessStandardResolver.Instance);
                var body = GetField(fluentdData, "body");
                if (includeFormattedMessage)
                {
                    Assert.Equal("Formatted Message", body);
                }
                else
                {
                    Assert.Null(body);
                }

                Assert.Equal("Value1", GetField(fluentdData, "Key1"));
                Assert.Equal("Value2", GetField(fluentdData, "Key2"));

                // ARRANGE
                logRecordList.Clear();

                // ACT
                // This is treated as Un-structured logging as the state cannot be converted to IReadOnlyList<KeyValuePair<string, object>>
                logger.Log(
                    LogLevel.Information,
                    default,
                    state: "somestringasdata",
                    exception: null,
                    formatter: (state, ex) => "Formatted Message");

                // VALIDATE
                Assert.Single(logRecordList);
                m_buffer = typeof(GenevaLogExporter).GetField("m_buffer", BindingFlags.NonPublic | BindingFlags.Static).GetValue(exporter) as ThreadLocal<byte[]>;
                _ = exporter.SerializeLogRecord(logRecordList[0]);
                fluentdData = MessagePack.MessagePackSerializer.Deserialize<object>(m_buffer.Value, MessagePack.Resolvers.ContractlessStandardResolver.Instance);
                body = GetField(fluentdData, "body");
                if (includeFormattedMessage)
                {
                    Assert.Equal("Formatted Message", body);
                }
                else
                {
                    Assert.Null(body);
                }

                // ARRANGE
                logRecordList.Clear();

                // ACT
                // This is treated as Un-structured logging as the state cannot be converted to IReadOnlyList<KeyValuePair<string, object>>
                logger.Log(
                    LogLevel.Information,
                    default,
                    state: "somestringasdata",
                    exception: null,
                    formatter: null);

                // VALIDATE
                Assert.Single(logRecordList);
                m_buffer = typeof(GenevaLogExporter).GetField("m_buffer", BindingFlags.NonPublic | BindingFlags.Static).GetValue(exporter) as ThreadLocal<byte[]>;
                _ = exporter.SerializeLogRecord(logRecordList[0]);
                fluentdData = MessagePack.MessagePackSerializer.Deserialize<object>(m_buffer.Value, MessagePack.Resolvers.ContractlessStandardResolver.Instance);
                body = GetField(fluentdData, "body");

                // Formatter is null, hence body is always null
                Assert.Null(body);

                // ARRANGE
                logRecordList.Clear();

                // ACT
                // This is treated as Structured logging as the state can be converted to IReadOnlyList<KeyValuePair<string, object>>
                logger.Log(
                    logLevel: LogLevel.Information,
                    eventId: default,
                    new List<KeyValuePair<string, object>>()
                        {
                            new KeyValuePair<string, object>("Key1", "Value1"),
                        },
                    exception: null,
                    formatter: (state, ex) => "Example formatted message.");

                // VALIDATE
                Assert.Single(logRecordList);
                m_buffer = typeof(GenevaLogExporter).GetField("m_buffer", BindingFlags.NonPublic | BindingFlags.Static).GetValue(exporter) as ThreadLocal<byte[]>;
                _ = exporter.SerializeLogRecord(logRecordList[0]);
                fluentdData = MessagePack.MessagePackSerializer.Deserialize<object>(m_buffer.Value, MessagePack.Resolvers.ContractlessStandardResolver.Instance);
                Assert.Equal("Value1", GetField(fluentdData, "Key1"));

                body = GetField(fluentdData, "body");

                // Only populate body if FormattedMessage is enabled
                if (includeFormattedMessage)
                {
                    Assert.Equal("Example formatted message.", body);
                }
                else
                {
                    Assert.Null(body);
                }
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

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, false, true)]
        [InlineData(true, true, true)]
        public void SerializationTestWithILoggerLogWithTemplates(bool hasTableNameMapping, bool hasCustomFields, bool parseStateValues)
        {
            string path = string.Empty;
            Socket server = null;
            var logRecordList = new List<LogRecord>();
            try
            {
                var exporterOptions = new GenevaExporterOptions
                {
                    PrepopulatedFields = new Dictionary<string, object>
                    {
                        ["cloud.role"] = "BusyWorker",
                        ["cloud.roleInstance"] = "CY1SCH030021417",
                        ["cloud.roleVer"] = "9.0.15289.2",
                    },
                };

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

                if (hasTableNameMapping)
                {
                    exporterOptions.TableNameMappings = new Dictionary<string, string>
                    {
                        { typeof(GenevaLogExporterTests).FullName, "CustomLogRecord" },
                        { "*", "DefaultLogRecord" },
                    };
                }

                if (hasCustomFields)
                {
                    // The field "customField" of LogRecord.State should be present in the mapping as a separate key. Other fields of LogRecord.State which are not present
                    // in CustomFields should be added in the mapping under "env_properties"
                    exporterOptions.CustomFields = new string[] { "customField" };
                }

                using var loggerFactory = LoggerFactory.Create(builder => builder
                .AddOpenTelemetry(options =>
                {
                    options.AddGenevaLogExporter(options =>
                    {
                        options.ConnectionString = exporterOptions.ConnectionString;
                        options.PrepopulatedFields = exporterOptions.PrepopulatedFields;
                    });
                    options.AddInMemoryExporter(logRecordList);
                    options.ParseStateValues = parseStateValues;
                })
                .AddFilter(typeof(GenevaLogExporterTests).FullName, LogLevel.Trace)); // Enable all LogLevels

                // Create a test exporter to get MessagePack byte data to validate if the data was serialized correctly.
                using var exporter = new GenevaLogExporter(exporterOptions);

                // Emit a LogRecord and grab a copy of the LogRecord from the collection passed to InMemoryExporter
                var logger = loggerFactory.CreateLogger<GenevaLogExporterTests>();

                // Set the ActivitySourceName to the unique value of the test method name to avoid interference with
                // the ActivitySource used by other unit tests.
                var sourceName = GetTestMethodName();

                using var listener = new ActivityListener();
                listener.ShouldListenTo = (activitySource) => activitySource.Name == sourceName;
                listener.Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded;
                ActivitySource.AddActivityListener(listener);

                using var source = new ActivitySource(sourceName);

                using (var activity = source.StartActivity("Activity"))
                {
                    // Log inside an activity to set LogRecord.TraceId and LogRecord.SpanId
                    logger.LogInformation("Hello from {food} {price}.", "artichoke", 3.99); // structured logging
                }

                // When the exporter options are configured with TableMappings only "customField" will be logged as a separate key in the mapping
                // "property" will be logged under "env_properties" in the mapping
                logger.Log(LogLevel.Trace, 101, "Log a {customField} and {property}", "CustomFieldValue", "PropertyValue");
                logger.Log(LogLevel.Trace, 101, "Log a {customField} and {property}", "CustomFieldValue", null);
                logger.Log(LogLevel.Trace, 101, "Log a {customField} and {property}", null, "PropertyValue");
                logger.Log(LogLevel.Debug, 101, "Log a {customField} and {property}", "CustomFieldValue", "PropertyValue");
                logger.Log(LogLevel.Information, 101, "Log a {customField} and {property}", "CustomFieldValue", "PropertyValue");
                logger.Log(LogLevel.Warning, 101, "Log a {customField} and {property}", "CustomFieldValue", "PropertyValue");
                logger.Log(LogLevel.Error, 101, "Log a {customField} and {property}", "CustomFieldValue", "PropertyValue");
                logger.Log(LogLevel.Critical, 101, "Log a {customField} and {property}", "CustomFieldValue", "PropertyValue");
                logger.LogInformation("Hello World!"); // unstructured logging
                logger.LogError(new InvalidOperationException("Oops! Food is spoiled!"), "Hello from {food} {price}.", "artichoke", 3.99);

                // Exception with a non-ASCII character in its type name
                logger.LogError(new CustomException\u0418(), "Hello from {food} {price}.", "artichoke", 3.99);

                var loggerWithDefaultCategory = loggerFactory.CreateLogger("DefaultCategory");
                loggerWithDefaultCategory.LogInformation("Basic test");
                loggerWithDefaultCategory.LogInformation("\u0418"); // Include non-ASCII characters in the message

                // logRecordList should have 14 logRecord entries as there were 14 Log calls
                Assert.Equal(14, logRecordList.Count);

                var m_buffer = typeof(GenevaLogExporter).GetField("m_buffer", BindingFlags.NonPublic | BindingFlags.Static).GetValue(exporter) as ThreadLocal<byte[]>;

                foreach (var logRecord in logRecordList)
                {
                    _ = exporter.SerializeLogRecord(logRecord);
                    object fluentdData = MessagePack.MessagePackSerializer.Deserialize<object>(m_buffer.Value, MessagePack.Resolvers.ContractlessStandardResolver.Instance);
                    this.AssertFluentdForwardModeForLogRecord(exporterOptions, fluentdData, logRecord);
                }
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

        [Fact]
        public void SuccessfulExport_Windows()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var exporterOptions = new GenevaExporterOptions()
                {
                    PrepopulatedFields = new Dictionary<string, object>
                    {
                        ["cloud.role"] = "BusyWorker",
                        ["cloud.roleInstance"] = "CY1SCH030021417",
                        ["cloud.roleVer"] = "9.0.15289.2",
                    },
                };

                using var loggerFactory = LoggerFactory.Create(builder => builder
                .AddOpenTelemetry(options =>
                {
                    options.AddGenevaLogExporter(options =>
                    {
                        options.ConnectionString = "EtwSession=OpenTelemetry";
                        options.PrepopulatedFields = new Dictionary<string, object>
                        {
                            ["cloud.role"] = "BusyWorker",
                            ["cloud.roleInstance"] = "CY1SCH030021417",
                            ["cloud.roleVer"] = "9.0.15289.2",
                        };
                    });
                }));

                var logger = loggerFactory.CreateLogger<GenevaLogExporterTests>();

                logger.LogInformation("Hello from {food} {price}.", "artichoke", 3.99);
            }
        }

        [Fact]
        public void SuccessfulExportOnLinux()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string path = GenerateTempFilePath();
                var logRecordList = new List<LogRecord>();
                try
                {
                    var endpoint = new UnixDomainSocketEndPoint(path);
                    using var server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                    server.Bind(endpoint);
                    server.Listen(1);

                    using var loggerFactory = LoggerFactory.Create(builder => builder
                    .AddOpenTelemetry(options =>
                    {
                        options.AddGenevaLogExporter(options =>
                        {
                            options.ConnectionString = "Endpoint=unix:" + path;
                            options.PrepopulatedFields = new Dictionary<string, object>
                            {
                                ["cloud.role"] = "BusyWorker",
                                ["cloud.roleInstance"] = "CY1SCH030021417",
                                ["cloud.roleVer"] = "9.0.15289.2",
                            };
                        });
                        options.AddInMemoryExporter(logRecordList);
                    }));
                    using var serverSocket = server.Accept();
                    serverSocket.ReceiveTimeout = 10000;

                    // Create a test exporter to get MessagePack byte data for validation of the data received via Socket.
                    using var exporter = new GenevaLogExporter(new GenevaExporterOptions
                    {
                        ConnectionString = "Endpoint=unix:" + path,
                        PrepopulatedFields = new Dictionary<string, object>
                        {
                            ["cloud.role"] = "BusyWorker",
                            ["cloud.roleInstance"] = "CY1SCH030021417",
                            ["cloud.roleVer"] = "9.0.15289.2",
                        },
                    });

                    // Emit a LogRecord and grab a copy of internal buffer for validation.
                    var logger = loggerFactory.CreateLogger<GenevaLogExporterTests>();

                    logger.LogInformation("Hello from {food} {price}.", "artichoke", 3.99);

                    // logRecordList should have a singleLogRecord entry after the logger.LogInformation call
                    Assert.Single(logRecordList);

                    int messagePackDataSize;
                    messagePackDataSize = exporter.SerializeLogRecord(logRecordList[0]);

                    // Read the data sent via socket.
                    var receivedData = new byte[1024];
                    int receivedDataSize = serverSocket.Receive(receivedData);

                    // Validation
                    Assert.Equal(messagePackDataSize, receivedDataSize);

                    logRecordList.Clear();

                    // Emit log on a different thread to test for multithreading scenarios
                    var thread = new Thread(() =>
                    {
                        logger.LogInformation("Hello from another thread {food} {price}.", "artichoke", 3.99);
                    });
                    thread.Start();
                    thread.Join();

                    // logRecordList should have a singleLogRecord entry after the logger.LogInformation call
                    Assert.Single(logRecordList);

                    messagePackDataSize = exporter.SerializeLogRecord(logRecordList[0]);
                    receivedDataSize = serverSocket.Receive(receivedData);
                    Assert.Equal(messagePackDataSize, receivedDataSize);
                }
                finally
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch
                    {
                    }
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

        private static string GetTestMethodName([CallerMemberName] string callingMethodName = "")
        {
            return callingMethodName;
        }

        private static object GetField(object fluentdData, string key)
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

            if (mapping.ContainsKey(key))
            {
                return mapping[key];
            }
            else
            {
                return null;
            }
        }

        private void AssertFluentdForwardModeForLogRecord(GenevaExporterOptions exporterOptions, object fluentdData, LogRecord logRecord)
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

            var signal = (fluentdData as object[])[0] as string;
            var TimeStampAndMappings = ((fluentdData as object[])[1] as object[])[0];
            var timeStamp = (DateTime)(TimeStampAndMappings as object[])[0];
            var mapping = (TimeStampAndMappings as object[])[1] as Dictionary<object, object>;
            var timeFormat = (fluentdData as object[])[2] as Dictionary<object, object>;

            var partAName = "Log";
            if (exporterOptions.TableNameMappings != null)
            {
                if (exporterOptions.TableNameMappings.ContainsKey(logRecord.CategoryName))
                {
                    partAName = exporterOptions.TableNameMappings[logRecord.CategoryName];
                }
                else if (exporterOptions.TableNameMappings.ContainsKey("*"))
                {
                    partAName = exporterOptions.TableNameMappings["*"];
                }
            }

            Assert.Equal(partAName, signal);

            // Timestamp check
            Assert.Equal(logRecord.Timestamp.Ticks, timeStamp.Ticks);

            // Part A core envelope fields

            var nameKey = GenevaBaseExporter<LogRecord>.V40_PART_A_MAPPING[Schema.V40.PartA.Name];

            // Check if the user has configured a custom table mapping
            Assert.Equal(partAName, mapping[nameKey]);

            // TODO: Update this when we support multiple Schema formats
            var partAVer = "4.0";
            var verKey = GenevaBaseExporter<LogRecord>.V40_PART_A_MAPPING[Schema.V40.PartA.Ver];
            Assert.Equal(partAVer, mapping[verKey]);

            foreach (var item in exporterOptions.PrepopulatedFields)
            {
                var partAValue = item.Value as string;
                var partAKey = GenevaBaseExporter<LogRecord>.V40_PART_A_MAPPING[item.Key];
                Assert.Equal(partAValue, mapping[partAKey]);
            }

            var timeKey = GenevaBaseExporter<LogRecord>.V40_PART_A_MAPPING[Schema.V40.PartA.Time];
            Assert.Equal(logRecord.Timestamp.Ticks, ((DateTime)mapping[timeKey]).Ticks);

            // Part A dt extensions

            if (logRecord.TraceId != default)
            {
                Assert.Equal(logRecord.TraceId.ToHexString(), mapping["env_dt_traceId"]);
            }

            if (logRecord.SpanId != default)
            {
                Assert.Equal(logRecord.SpanId.ToHexString(), mapping["env_dt_spanId"]);
            }

            if (logRecord.Exception != null)
            {
                Assert.Equal(logRecord.Exception.GetType().FullName, mapping["env_ex_type"]);
                Assert.Equal(logRecord.Exception.Message, mapping["env_ex_msg"]);
            }

            // Part B fields
            Assert.Equal(logRecord.LogLevel.ToString(), mapping["severityText"]);
            Assert.Equal((byte)(((int)logRecord.LogLevel * 4) + 1), mapping["severityNumber"]);

            Assert.Equal(logRecord.CategoryName, mapping["name"]);

            bool isUnstructuredLog = true;
            IReadOnlyList<KeyValuePair<string, object>> stateKeyValuePairList;
            if (logRecord.State == null)
            {
                stateKeyValuePairList = logRecord.StateValues;
            }
            else
            {
                stateKeyValuePairList = logRecord.State as IReadOnlyList<KeyValuePair<string, object>>;
            }

            if (stateKeyValuePairList != null)
            {
                isUnstructuredLog = stateKeyValuePairList.Count == 1;
            }

            if (isUnstructuredLog)
            {
                if (logRecord.State != null)
                {
                    Assert.Equal(logRecord.State.ToString(), mapping["body"]);
                }
                else
                {
                    Assert.Equal(stateKeyValuePairList[0].Value, mapping["body"]);
                }
            }
            else
            {
                _ = mapping.TryGetValue("env_properties", out object envProprties);
                var envPropertiesMapping = envProprties as IDictionary<object, object>;

                foreach (var item in stateKeyValuePairList)
                {
                    if (item.Key == "{OriginalFormat}")
                    {
                        Assert.Equal(item.Value.ToString(), mapping["body"]);
                    }
                    else if (exporterOptions.CustomFields == null || exporterOptions.CustomFields.Contains(item.Key))
                    {
                        if (item.Value != null)
                        {
                            Assert.Equal(item.Value, mapping[item.Key]);
                        }
                    }
                    else
                    {
                        Assert.Equal(item.Value, envPropertiesMapping[item.Key]);
                    }
                }
            }

            if (logRecord.EventId != default)
            {
                Assert.Equal(logRecord.EventId.Id, int.Parse(mapping["eventId"].ToString()));
            }

            // Epilouge
            Assert.Equal("DateTime", timeFormat["TimeFormat"]);
        }

        // A custom exception class with non-ASCII character in the type name
        private class CustomException\u0418 : Exception
        {
        }
    }
}
