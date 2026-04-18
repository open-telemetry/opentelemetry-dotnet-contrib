// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal.Utils;
using OpenTelemetry.OpAmp.Client.Tests.Mocks;

namespace OpenTelemetry.OpAmp.Client.Tests.Tools;

internal class FrameGenerator
{
    private static readonly int WebSocketHeaderSize = OpAmpWsHeaderHelper.WriteHeader(new ArraySegment<byte>(new byte[OpAmpWsHeaderHelper.MaxHeaderLength]));

    public static MockAgentFrame GenerateMockAgentFrame(bool useSmallPackets = true)
    {
        var content = "This is a mock agent frame for testing purposes.";
        if (!useSmallPackets)
        {
            var padding = new string('A', 100_000);
            content = string.Concat(content, "__", padding);
        }

        var uid = ByteString.CopyFrom(Guid.NewGuid().ToByteArray());
        var frame = new AgentToServer()
        {
            InstanceUid = uid,
            CustomMessage = new CustomMessage()
            {
                Data = ByteString.CopyFromUtf8(content),
                Type = "Utf8String",
            },
        };

        return new MockAgentFrame(uid, frame);
    }

    public static MockServerFrame GenerateMockServerFrame(ByteString? instanceUid = null, bool useSmallPackets = true, bool addHeader = false)
    {
        var content = "This is a mock server frame for testing purposes.";
        if (!useSmallPackets)
        {
            var padding = new string('A', 100_000);
            content = string.Concat(content, "__", padding);
        }

        var uid = instanceUid ?? ByteString.CopyFrom(Guid.NewGuid().ToByteArray());

        var frame = new ServerToAgent
        {
            InstanceUid = uid,
            CustomMessage = new CustomMessage()
            {
                Data = ByteString.CopyFromUtf8(content),
                Type = "Utf8String",
            },
        };

        var result = SerializeServerFrame(frame, addHeader);
        result.ExpectedContent = content;
        return result;
    }

    public static MockServerFrame GenerateMockServerFrameOfTotalSize(int totalSize, bool addHeader = false)
    {
        for (var typeLength = 1; typeLength <= 64; typeLength++)
        {
            var frame = TryGenerateMockServerFrameOfTotalSize(totalSize, addHeader, typeLength);
            if (frame != null)
            {
                return frame;
            }
        }

        throw new InvalidOperationException($"Unable to generate a valid server frame of total size {totalSize}.");
    }

    private static MockServerFrame? TryGenerateMockServerFrameOfTotalSize(int totalSize, bool addHeader, int typeLength)
    {
        var uid = ByteString.CopyFrom(Guid.NewGuid().ToByteArray());
        var type = new string('T', typeLength);
        var low = 0;
        var high = totalSize;

        while (low <= high)
        {
            var dataLength = low + ((high - low) / 2);
            var frame = new ServerToAgent
            {
                InstanceUid = uid,
                CustomMessage = new CustomMessage
                {
                    Data = ByteString.CopyFrom(new byte[dataLength]),
                    Type = type,
                },
            };

            var totalFrameSize = frame.CalculateSize() + (addHeader ? WebSocketHeaderSize : 0);
            if (totalFrameSize == totalSize)
            {
                return SerializeServerFrame(frame, addHeader);
            }

            if (totalFrameSize < totalSize)
            {
                low = dataLength + 1;
            }
            else
            {
                high = dataLength - 1;
            }
        }

        return null;
    }

    private static MockServerFrame SerializeServerFrame(ServerToAgent frame, bool addHeader)
    {
        var size = frame.CalculateSize();
        var responseBuffer = addHeader
            ? new byte[size + WebSocketHeaderSize]
            : new byte[size];
        ArraySegment<byte> responseSegment;

        if (addHeader)
        {
            var headerSize = OpAmpWsHeaderHelper.WriteHeader(new ArraySegment<byte>(responseBuffer));
            var segment = new ArraySegment<byte>(responseBuffer, headerSize, size);
            frame.WriteTo(segment);
            responseSegment = new ArraySegment<byte>(responseBuffer, 0, size + headerSize);
        }
        else
        {
            frame.WriteTo(responseBuffer);
            responseSegment = new ArraySegment<byte>(responseBuffer, 0, size);
        }

        return new MockServerFrame
        {
            Frame = responseSegment,
            Size = size,
            HasHeader = addHeader,
        };
    }
}
