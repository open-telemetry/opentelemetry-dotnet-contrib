// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;

namespace OpenTelemetry.Internal;

public static class RedactionGenerators
{
    private static readonly char[] QueryAlphabet = ['a', 'b', '=', '=', '&', '&', '?', ';', '#', '%', ' ', '/'];
    private static readonly char[] ValueAlphabet = ['a', 'b', '=', '?', ';', '#', '%', ' ', '/', ':', '+', '"'];
    private static readonly char[] KeyAlphabet = ['a', 'b', '?', ';', '#', '%', ' ', '/', '[', ']'];

    public static Arbitrary<QueryText> QueryTextArbitrary()
    {
        var gen = from text in StringOf(QueryAlphabet, 0, 24) select new QueryText(text);

        return gen.ToArbitrary();
    }

    public static Arbitrary<EmbeddedQuery> EmbeddedQueryArbitrary()
    {
        var gen =
            from prefix in StringOf(QueryAlphabet, 0, 8)
            from query in StringOf(QueryAlphabet, 0, 24)
            from suffix in StringOf(QueryAlphabet, 0, 8)
            select new EmbeddedQuery(prefix, query, suffix);

        return gen.ToArbitrary();
    }

    public static Arbitrary<SecretQuery> SecretQueryArbitrary()
    {
        var parameterGen =
            from key in StringOf(KeyAlphabet, 0, 4)
            from before in StringOf(ValueAlphabet, 0, 4)
            from after in StringOf(ValueAlphabet, 0, 4)
            select (Key: key, Value: before + SecretQuery.Secret + after);

        var gen =
            from count in Gen.Choose(1, 5)
            from parameters in parameterGen.ListOf(count)
            select new SecretQuery(string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}")));

        return gen.ToArbitrary();
    }

    private static Gen<string> StringOf(char[] alphabet, int minLength, int maxLength) =>
        from length in Gen.Choose(minLength, maxLength)
        from chars in Gen.Elements(alphabet).ListOf(length)
        select new string([.. chars]);
}
