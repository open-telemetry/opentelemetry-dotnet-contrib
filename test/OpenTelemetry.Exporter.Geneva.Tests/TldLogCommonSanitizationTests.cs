// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.Geneva.Tld;

namespace OpenTelemetry.Exporter.Geneva.Tests;

public class TldLogCommonSanitizationTests
{
    // categoryName -> expected sanitized event name:
    //  - lower-case first character is upper-cased,
    //  - non-alphanumeric characters are stripped,
    //  - a non-letter first character yields an empty name.
    [Theory]
    [InlineData("testLogger", "TestLogger")]
    [InlineData("TestCompany.TestNamespace.TestLogger", "TestCompanyTestNamespaceTestLogger")]
    [InlineData("Foo-Bar_Baz 123", "FooBarBaz123")]
    [InlineData("123abc", "")]
    [InlineData("_invalid", "")]
    [InlineData(".dotted", "")]
    public void GetSanitizedCategoryName_SanitizesAsExpected(string categoryName, string expected)
    {
        using var probe = CreateProbe();

        Assert.Equal(expected, probe.Sanitize(categoryName));
    }

    // Already-valid names (start with an upper-case letter, alphanumeric only,
    // <= 50 chars) take the allocation-free fast path and are returned verbatim.
    [Theory]
    [InlineData("TestLogger")]
    [InlineData("ABC123")]
    [InlineData("A")]
    public void GetSanitizedCategoryName_AlreadyValid_ReturnsSameReference(string categoryName)
    {
        using var probe = CreateProbe();

        var result = probe.Sanitize(categoryName);

        Assert.Same(categoryName, result);
    }

    [Fact]
    public void GetSanitizedCategoryName_TruncatesToMaxLength()
    {
        using var probe = CreateProbe();

        // 60 characters, with a lower-case leading char that forces the slower
        // sanitize path (and is itself up-cased).
        var categoryName = "a" + new string('b', 59);
        var expected = "A" + new string('b', 49); // 50 characters total.

        var result = probe.Sanitize(categoryName);

        Assert.Equal(50, result.Length);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetSanitizedCategoryName_ExactlyMaxLengthValidName_ReturnsSameReference()
    {
        using var probe = CreateProbe();

        var categoryName = "A" + new string('B', 49); // 50 characters, all valid.

        var result = probe.Sanitize(categoryName);

        Assert.Same(categoryName, result);
    }

    [Fact]
    public void GetSanitizedCategoryName_CachesSanitizedResult()
    {
        using var probe = CreateProbe();

        const string categoryName = "Test.Namespace.Logger";

        var first = probe.Sanitize(categoryName);
        var second = probe.Sanitize(categoryName);

        Assert.Equal("TestNamespaceLogger", first);

        // The second lookup must hit the cache and return the very same string
        // instance produced by the first call rather than re-sanitizing.
        Assert.Same(first, second);
    }

    [Fact]
    public void GetSanitizedCategoryName_AtMaxCachedLength_IsCached()
    {
        using var probe = CreateProbe();

        var categoryName = "a" + new string('b', CategorySanitizerProbe.MaxCachedCategoryNameLengthValue - 1);
        var expected = "A" + new string('b', 49);

        var first = probe.Sanitize(categoryName);
        var second = probe.Sanitize(categoryName);

        Assert.Equal(expected, first);
        Assert.Same(first, second);
    }

    [Fact]
    public void GetSanitizedCategoryName_ExceedsMaxCachedLength_IsNotCachedButStillSanitized()
    {
        using var probe = CreateProbe();

        var categoryName = "a" + new string('b', CategorySanitizerProbe.MaxCachedCategoryNameLengthValue);
        var expected = "A" + new string('b', 49);

        var first = probe.Sanitize(categoryName);
        var second = probe.Sanitize(categoryName);

        Assert.Equal(expected, first);
        Assert.Equal(expected, second);
        Assert.NotSame(first, second);
    }

    private static CategorySanitizerProbe CreateProbe()
    {
        return new CategorySanitizerProbe(new GenevaExporterOptions
        {
            ConnectionString = "EtwSession=OpenTelemetry;PrivatePreviewEnableTraceLoggingDynamic=true",
            TableNameMappings = new Dictionary<string, string>
            {
                ["*"] = "*",
            },
        });
    }

    // Minimal TldLogCommon subclass that exposes the protected sanitization entry
    // point so the cache + fast-path can be exercised in isolation, independent of
    // the platform-specific TLD transport.
    private sealed class CategorySanitizerProbe : TldLogCommon
    {
        public CategorySanitizerProbe(GenevaExporterOptions options)
            : base(options)
        {
        }

        public static int MaxCachedCategoryNameLengthValue => MaxCachedCategoryNameLength;

        public string Sanitize(string categoryName)
            => this.GetSanitizedCategoryName(categoryName);
    }
}
