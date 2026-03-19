// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Instrumentation.Tests;

public class RenovateSchemaTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public async Task Renovate_Configuration_Is_Valid()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        var schemaUri = new Uri("https://docs.renovatebot.com/renovate-schema.json");

        using var stream = typeof(RenovateSchemaTests).Assembly.GetManifestResourceStream("OpenTelemetry.Contrib.Shared.Tests.renovate.json")!;
        using var reader = new StreamReader(stream);
        var configurationJson = await reader.ReadToEndAsync(cancellationToken);

        AssertJson(configurationJson);

        using var client = new HttpClient();
        var schemaJson = await client.GetStringAsync(schemaUri, cancellationToken);
        var schema = JSchema.Parse(
            schemaJson,
            new JSchemaReaderSettings() { ValidateVersion = true });

        var configuration = JToken.Parse(configurationJson);

        // Act
        var actual = configuration.IsValid(schema, out IList<ValidationError> errors);

        // Assert
        Assert.NotNull(errors);

        outputHelper.WriteLine(string.Join(Environment.NewLine, errors.Select((p) => FormatValidationError(p))));

        Assert.Empty(errors);
        Assert.True(actual);

        AssertRegularExpressions(configuration, "$");

        static string FormatValidationError(ValidationError error, string indent = "")
        {
            var builder = new StringBuilder();

            Format(error, builder, indent);

            return builder.ToString();

            static void Format(ValidationError error, StringBuilder builder, string indent = "")
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"{indent} Message: {error.Message}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    Path: {error.Path}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}SchemaId: {error.SchemaId}");

                if (error.Value != null)
                {
                    builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}   Value: {error.Value}");
                }

                foreach (var child in error.ChildErrors)
                {
                    Format(child, builder, indent + "  ");
                }
            }
        }
    }

    private static void AssertJson(string json)
    {
        try
        {
            System.Text.Json.JsonDocument.Parse(json);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Invalid JSON: {ex}");
        }
    }

    private static void AssertRegularExpressions(JToken token, string path)
    {
        if (token.Type == JTokenType.Object)
        {
            foreach (var property in token.Children<JProperty>())
            {
                AssertRegularExpressions(property.Value, $"{path}/{property.Name}");
            }
        }
        else if (token.Type == JTokenType.Array)
        {
            int index = 0;

            foreach (var item in token.Children())
            {
                AssertRegularExpressions(item, $"{path}[{index}]");
                index++;
            }
        }
        else if (token.Type == JTokenType.String)
        {
            var value = token.Value<string>();

            if (value?.Length > 2 && value[0] == '/' && value[^1] == '/')
            {
                try
                {
                    _ = new Regex(value[1..^1]);
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Invalid regular expression at path '{path}': {value}. Exception: {ex}");
                }
            }
        }
    }
}
#endif
