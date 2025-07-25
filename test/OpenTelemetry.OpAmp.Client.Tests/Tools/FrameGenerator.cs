// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using Google.Protobuf;
using OpAmp.Protocol;
using OpenTelemetry.OpAmp.Client.Tests.Mocks;

namespace OpenTelemetry.OpAmp.Client.Tests.Tools;

internal class FrameGenerator
{
    public static MockFrame GenerateMockFrame()
    {
        var content = "This is a mock frame for testing purposes.";
        var frame = new ServerToAgent
        {
            InstanceUid = ByteString.CopyFrom(Guid.NewGuid().ToByteArray()),
            CustomMessage = new CustomMessage()
            {
                Data = ByteString.CopyFromUtf8(content),
                Type = "Utf8String",
            },
        };

        return new MockFrame
        {
            Frame = new ReadOnlySequence<byte>(frame.ToByteArray()),
            HasHeader = false,
            ExptectedContent = content,
        };
    }
}
