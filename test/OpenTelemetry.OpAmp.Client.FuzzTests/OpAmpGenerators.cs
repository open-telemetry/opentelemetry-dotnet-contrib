// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;

namespace OpenTelemetry.OpAmp.Client.FuzzTests;

public static class OpAmpGenerators
{
    public static Arbitrary<ServerFrame> ServerFrameArbitrary() =>
        Gen.Choose(0, int.MaxValue).Select(seed => new ServerFrame(seed)).ToArbitrary();

    public static Arbitrary<DamagedFrame> DamagedFrameArbitrary() =>
        Gen.Choose(0, int.MaxValue).Select(seed => new DamagedFrame(seed)).ToArbitrary();
}
