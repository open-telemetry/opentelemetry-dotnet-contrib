// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;

namespace OpenTelemetry.Instrumentation;

public static class SqlGenerators
{
    public static Arbitrary<FuzzedSql> FuzzedSqlArbitrary() =>
        Gen.Choose(0, int.MaxValue).Select(seed => new FuzzedSql(seed)).ToArbitrary();

    public static Arbitrary<SqlSoup> SqlSoupArbitrary() =>
        Gen.Choose(0, int.MaxValue).Select(seed => new SqlSoup(seed)).ToArbitrary();
}
