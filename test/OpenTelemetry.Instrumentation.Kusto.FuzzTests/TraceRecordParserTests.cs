// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Xunit;
using OpenTelemetry.Instrumentation.Kusto.Implementation;

namespace OpenTelemetry.Instrumentation.Kusto.FuzzTests;

public static class TraceRecordParserTests
{
    private const int MaxTest = 1_000;

    [Property(MaxTest = MaxTest)]
    public static void ParseRequestStart_Arbitrary_Input_Does_Not_Throw(NonEmptyString input)
    {
        // The parser returns a ref struct, which cannot cross a lambda boundary, so it is called directly;
        // an exception escaping the call fails the property.
        var parsed = TraceRecordParser.ParseRequestStart(input.Get.AsSpan(), input.Get.AsSpan());

        _ = parsed.QueryText.Length;
    }

    [Property(MaxTest = MaxTest)]
    public static void ParseException_Arbitrary_Input_Does_Not_Throw(NonEmptyString input)
    {
        var parsed = TraceRecordParser.ParseException(input.Get.AsSpan());

        _ = parsed.ErrorMessage.Length;
    }

    [Property(MaxTest = MaxTest)]
    public static void ParseRequestStart_Extracts_Trailing_Query_Text(NonEmptyString input)
    {
        // text= is documented to be the last field, so everything after it is the query text verbatim.
        var query = input.Get.Replace("\r", " ").Replace("\n", " ");
        var message = $"$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=https://cluster.kusto.windows.net/, DatabaseName=NetDefaultDB, ClientRequestId=KD2RunQuery;1, text={query}";

        var parsed = TraceRecordParser.ParseRequestStart(message.AsSpan(), "KD2RunQuery;1".AsSpan());

        Assert.Equal(query, parsed.QueryText.ToString());
    }

    [Property(MaxTest = MaxTest)]
    public static void ParseRequestStart_Query_Text_Is_Not_Taken_From_Client_Request_Id(NonEmptyString input)
    {
        // The client request id is written unescaped ahead of the query text; whatever it contains
        // (including ", text="), the query text is the field that follows it.
        var clientRequestId = input.Get;
        var message = $"$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=https://cluster.kusto.windows.net/, DatabaseName=NetDefaultDB, ClientRequestId={clientRequestId}, text=StormEvents | take 10";

        var parsed = TraceRecordParser.ParseRequestStart(message.AsSpan(), clientRequestId.AsSpan());

        Assert.Equal("StormEvents | take 10", parsed.QueryText.ToString());
    }

    [Property(MaxTest = MaxTest)]
    public static void ParseRequestStart_Unknown_Client_Request_Id_Never_Returns_Caller_Text(NonEmptyString input)
    {
        // Without the client request id the query text is either the real query or nothing,
        // whatever the caller-supplied client request id contains.
        var message = $"$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=https://cluster.kusto.windows.net/, DatabaseName=NetDefaultDB, ClientRequestId={input.Get}, text=StormEvents | take 10";

        var parsed = TraceRecordParser.ParseRequestStart(message.AsSpan(), [], requestBodyTraced: true);

        var queryText = parsed.QueryText.ToString();
        Assert.True(queryText.Length == 0 || queryText == "StormEvents | take 10");
    }

    [Property(MaxTest = MaxTest)]
    public static void ParseRequestStart_Extracts_Database(NonEmptyString input)
    {
        var database = new string([.. input.Get.Where(char.IsLetterOrDigit).Take(32)]);
        if (string.IsNullOrEmpty(database))
        {
            database = "NetDefaultDB";
        }

        var message = $"$$HTTPREQUEST[RestClient2]: DatabaseName={database}, text=StormEvents | take 10";

        var parsed = TraceRecordParser.ParseRequestStart(message.AsSpan(), []);

        Assert.Equal(database, parsed.Database.ToString());
    }
}
