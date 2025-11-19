// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Kusto.Implementation;
using Xunit;

namespace OpenTelemetry.Instrumentation.Kusto.Tests;

public class KustoQueryParserTests
{
    public static TheoryData<string, string?, string?> QuerySummaryTestCases => new()
    {
        // Null / empty / invalid
        // NOTE: In these cases, there's no objectively correct answer, so the main goal
        // is to ensure we handle error cases gracefully
        {
            string.Empty,
            string.Empty,
            string.Empty
        },
        {
            " \t\n ",
            string.Empty,
            " \t\n "
        },
        {
            "this is not a valid query @#$%",
            "this is a valid query",
            "this is not a valid query @#$%"
        },
        {
            "StormEvents |",
            "StormEvents |",
            "StormEvents |"
        },

        // Simple table reference
        {
            "StormEvents",
            "StormEvents",
            "StormEvents"
        },

        // Print statement
        {
            "print number=42",
            "print",
            "print number=?"
        },

        // Pipes
        {
            "StormEvents | where State == 'FLORIDA'",
            "StormEvents | where",
            "StormEvents | where State == ?"
        },
        {
            "StormEvents | project State, EventType",
            "StormEvents | project",
            "StormEvents | project State, EventType"
        },
        {
            "StormEvents | summarize count() by State",
            "StormEvents | summarize",
            "StormEvents | summarize count() by State"
        },
        {
            "StormEvents | where State == 'FLORIDA' | project State, EventType | take 10",
            "StormEvents | where | project | take",
            "StormEvents | where State == ? | project State, EventType | take ?"
        },
        {
            "StormEvents | where State == 'CA' | extend NewCol = 1 | project State | summarize count() | order by count_",
            "StormEvents | where | extend | project | summarize | order",
            "StormEvents | where State == ? | extend NewCol = ? | project State | summarize count() | order by count_"
        },

        // Database function
        {
            "database('SampleDB').StormEvents",
            "StormEvents",
            "database(?).StormEvents"
        },

        // Let statement
        {
            "let threshold = 5; StormEvents | where DamageProperty > threshold",
            "StormEvents | where",
            "let threshold = ?; StormEvents | where DamageProperty > threshold"
        },
        {
            "let x = 10; let y = 20; StormEvents | take x",
            "StormEvents | take",
            "let x = ?; let y = ?; StormEvents | take x"
        },

        // Nested queries
        {
            "StormEvents | union OtherEvents",
            "StormEvents | union OtherEvents",
            "StormEvents | union OtherEvents"
        },
        {
            "StormEvents | join kind=inner (PopulationData) on State",
            "StormEvents | join PopulationData",
            "StormEvents | join kind=? (PopulationData) on State"
        },
        {
            "let threshold = 1000;\nStormEvents\n| where DamageProperty > threshold\n| join kind=inner (\n    PopulationData\n    | where Year == 2020\n) on State\n| summarize TotalDamage = sum(DamageProperty) by State\n| top 10 by TotalDamage",
            "StormEvents | where | join PopulationData | where | summarize | top",
            "let threshold = ?;\nStormEvents\n| where DamageProperty > threshold\n| join kind=? (\n    PopulationData\n    | where Year == ?\n) on State\n| summarize TotalDamage = sum(DamageProperty) by State\n| top ? by TotalDamage"
        },
        {
            "StormEvents | union WeatherEvents | where State == 'TX'",
            "StormEvents | union WeatherEvents | where",
            "StormEvents | union WeatherEvents | where State == ?"
        },

        // Control command
        {
            ".show databases",
            ".show",
            ".show databases"
        },

        // Range
        {
            "range x from 1 to 10 step 1",
            "range",
            "range x from ? to ? step ?"
        },
        {
            "StormEvents | where Value between (10 .. 100)",
            "StormEvents | where",
            "StormEvents | where Value between (? .. ?)"
        },

        // DataTable
        {
            "datatable(name:string, age:int) ['Alice', 30, 'Bob', 25]",
            "datatable",
            "datatable(name:string, age:int) [?, ?, ?, ?]"
        },

        // Query with newlines
        {
            "StormEvents\n| where State == 'FLORIDA'\n| project State, EventType\n| take 10",
            "StormEvents | where | project | take",
            "StormEvents\n| where State == ?\n| project State, EventType\n| take ?"
        },

        // Query with tabs
        {
            "StormEvents\t|\twhere\tState\t==\t'FLORIDA'",
            "StormEvents | where",
            "StormEvents\t|\twhere\tState\t==\t?"
        },

        // Comments
        // NOTE: Ideally comments would be stripped from sanitized queries, but Kusto.Language does not easily allow for
        // stripping embedded comments, so codifying the behavior for now. If this becomes a problem we can revisit.
        {
            "// Single line comment\nStormEvents | where State == 'TX'",
            "StormEvents | where",
            "// Single line comment\nStormEvents | where State == ?"
        },
        {
            "StormEvents | take 10 // Get first 10 rows",
            "StormEvents | take",
            "StormEvents | take ? // Get first 10 rows"
        },

        // Number parsing
        {
            "StormEvents | where Temperature < -10",
            "StormEvents | where",
            "StormEvents | where Temperature < ?"
        },
        {
            "StormEvents | where Value == -42.5",
            "StormEvents | where",
            "StormEvents | where Value == ?"
        },
        {
            "StormEvents | where Count > +100",
            "StormEvents | where",
            "StormEvents | where Count > ?"
        },
        {
            "StormEvents | where Value > 1.5e10",
            "StormEvents | where",
            "StormEvents | where Value > ?"
        },
        {
            "StormEvents | where Value < 3.14E-5",
            "StormEvents | where",
            "StormEvents | where Value < ?"
        },
        {
            "StormEvents | where Value == -2.5e+8",
            "StormEvents | where",
            "StormEvents | where Value == ?"
        },
        {
            "StormEvents | where Price == 123.456",
            "StormEvents | where",
            "StormEvents | where Price == ?"
        },

        // Mixed string and numeric literals
        {
            "StormEvents | where State == 'FL' and Temp > 90",
            "StormEvents | where",
            "StormEvents | where State == ? and Temp > ?"
        },

        // Double-quoted strings
        {
            "print text = \"Hello World\"",
            "print",
            "print text = ?"
        },

        // Empty strings
        {
            "StormEvents | where State != ''",
            "StormEvents | where",
            "StormEvents | where State != ?"
        },

        // Nested parentheses
        {
            "StormEvents | where (State == 'CA' and (Temp > 80))",
            "StormEvents | where",
            "StormEvents | where (State == ? and (Temp > ?))"
        },

        // Boolean literals
        {
            "StormEvents | where IsActive == true",
            "StormEvents | where",
            "StormEvents | where IsActive == ?"
        },
        {
            "StormEvents | where IsDeleted == false",
            "StormEvents | where",
            "StormEvents | where IsDeleted == ?"
        },
        {
            "print flag=true",
            "print",
            "print flag=?"
        },
        {
            "StormEvents | extend Active = false",
            "StormEvents | extend",
            "StormEvents | extend Active = ?"
        },

        // DateTime literals
        {
            "StormEvents | where StartTime > datetime(2020-01-01)",
            "StormEvents | where",
            "StormEvents | where StartTime > ?"
        },
        {
            "StormEvents | where EventTime >= datetime('2021-05-15T10:30:00Z')",
            "StormEvents | where",
            "StormEvents | where EventTime >= ?"
        },
        {
            "StormEvents | where timestamp > datetime(2023-12-25)",
            "StormEvents | where",
            "StormEvents | where timestamp > ?"
        },

        // TimeSpan/duration literals
        {
            "StormEvents | where Duration > 1h",
            "StormEvents | where",
            "StormEvents | where Duration > ?"
        },
        {
            "StormEvents | where StartTime < ago(7d)",
            "StormEvents | where",
            "StormEvents | where StartTime < ago(?)"
        },
        {
            "StormEvents | where TimeDiff < 30m",
            "StormEvents | where",
            "StormEvents | where TimeDiff < ?"
        },
        {
            "StormEvents | where Duration == 2.5h",
            "StormEvents | where",
            "StormEvents | where Duration == ?"
        },

        // GUID literals
        {
            "StormEvents | where Id == guid(12345678-1234-1234-1234-123456789012)",
            "StormEvents | where",
            "StormEvents | where Id == ?"
        },
        {
            "Users | where UserId == guid('a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d')",
            "Users | where",
            "Users | where UserId == ?"
        },

        // Dynamic/JSON literals
        {
            "StormEvents | where Data == dynamic({\"key\":\"value\"})",
            "StormEvents | where",
            "StormEvents | where Data == ?"
        },
        {
            "StormEvents | extend Props = dynamic(['a', 'b', 'c'])",
            "StormEvents | extend",
            "StormEvents | extend Props = ?"
        },
        {
            "print obj=dynamic({\"x\":1,\"y\":2})",
            "print",
            "print obj=?"
        },

        // Binary/Hexadecimal literals
        {
            "StormEvents | where Flags == 0x1F",
            "StormEvents | where",
            "StormEvents | where Flags == ?"
        },
        {
            "StormEvents | where Mask == 0xFF00",
            "StormEvents | where",
            "StormEvents | where Mask == ?"
        },
        {
            "print hex=0xDEADBEEF",
            "print",
            "print hex=?"
        },

        // Interval literals
        {
            "StormEvents | where StartTime between (datetime(2007-07-27) .. ago(1d))",
            "StormEvents | where",
            "StormEvents | where StartTime between (? .. ago(?))"
        },

        // Materialized view
        // NOTE: Ideally the summarizer would strip "Condition", but it this context it is ambiguous with a table reference. Given the prevelance of this type of query,
        // codifying the behavior for now. If this becomes a problem we can revisit.
        {
            "database('*').materialized_view('ViewName') | where Condition == 'Value'",
            "materialized_view('ViewName') | where Condition",
            "database(?).materialized_view(?) | where Condition == ?"
        },

        // Long query - tests 255 character truncation
        {
            "StormEvents | where State == 'CALIFORNIA' | extend NewColumn1 = 1 | extend NewColumn2 = 2 | extend NewColumn3 = 3 | extend NewColumn4 = 4 | extend NewColumn5 = 5 | extend NewColumn6 = 6 | extend NewColumn7 = 7 | extend NewColumn8 = 8 | extend NewColumn9 = 9 | extend NewColumn10 = 10 | extend NewColumn11 = 11 | extend NewColumn12 = 12 | extend NewColumn13 = 13 | extend NewColumn14 = 14 | extend NewColumn15 = 15 | extend NewColumn16 = 16 | extend NewColumn17 = 17 | extend NewColumn18 = 18 | extend NewColumn19 = 19 | extend NewColumn20 = 20 | extend NewColumn21 = 21 | extend NewColumn22 = 22 | extend NewColumn23 = 23 | extend NewColumn24 = 24 | extend NewColumn25 = 25 | extend NewColumn26 = 26 | extend NewColumn27 = 27",
            "StormEvents | where | extend | extend | extend | extend | extend | extend | extend | extend | extend | extend | extend | extend | extend | extend | extend | extend | extend | extend | extend | extend | extend | extend | extend | extend | extend | extend |",
            "StormEvents | where State == ? | extend NewColumn1 = ? | extend NewColumn2 = ? | extend NewColumn3 = ? | extend NewColumn4 = ? | extend NewColumn5 = ? | extend NewColumn6 = ? | extend NewColumn7 = ? | extend NewColumn8 = ? | extend NewColumn9 = ? | extend NewColumn10 = ? | extend NewColumn11 = ? | extend NewColumn12 = ? | extend NewColumn13 = ? | extend NewColumn14 = ? | extend NewColumn15 = ? | extend NewColumn16 = ? | extend NewColumn17 = ? | extend NewColumn18 = ? | extend NewColumn19 = ? | extend NewColumn20 = ? | extend NewColumn21 = ? | extend NewColumn22 = ? | extend NewColumn23 = ? | extend NewColumn24 = ? | extend NewColumn25 = ? | extend NewColumn26 = ? | extend NewColumn27 = ?"
        },
    };

    [Theory]
    [MemberData(nameof(QuerySummaryTestCases))]
    public void GenerateQuerySummary_ReturnsExpectedSummary(string query, string? expectedSummary, string? expectedSanitizedQuery)
    {
        var info = KustoProcessor.Process(shouldSummarize: true, shouldSanitize: true, query);

        Assert.Equal(expectedSummary, info.Summarized);
        Assert.Equal(expectedSanitizedQuery, info.Sanitized);
    }
}
