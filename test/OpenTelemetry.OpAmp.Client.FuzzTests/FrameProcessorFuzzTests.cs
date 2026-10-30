// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck.Xunit;
using Google.Protobuf;
using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Utils;

namespace OpenTelemetry.OpAmp.Client.FuzzTests;

/// <summary>
/// Property-based (fuzz) tests that assert a message from the OpAMP server is dispatched to listeners as it was sent,
/// however it is split between buffers, and that a message which was damaged on its way is rejected without
/// anything worse than a protocol buffer exception, which the transports are written to handle.
/// </summary>
public static class FrameProcessorFuzzTests
{
    private const int MaxTest = 1_000;

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(OpAmpGenerators)])]
    public static void OnServerFrame_Dispatches_A_Message_As_It_Was_Sent(ServerFrame frame, int splitSeed)
    {
        var processor = new FrameProcessor();
        var recorder = new ServerMessageRecorder(processor);

        processor.OnServerFrame(TestSegment.Split(frame.Bytes, splitSeed));

        recorder.AssertDispatched(frame.Message);
    }

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(OpAmpGenerators)])]
    public static void OnServerFrame_Dispatches_A_WebSocket_Message_As_It_Was_Sent(ServerFrame frame, int splitSeed)
    {
        var processor = new FrameProcessor();
        var recorder = new ServerMessageRecorder(processor);
        var data = WithHeader(0, frame.Bytes);

        processor.OnServerFrame(TestSegment.Split(data, splitSeed), data.Length, verifyHeader: true);

        recorder.AssertDispatched(frame.Message);
    }

    [Property(MaxTest = MaxTest)]
    public static void OnServerFrame_Preserves_Every_Bit_Of_The_Flags_And_Capabilities(uint high, uint low, byte shift, bool swap, int splitSeed)
    {
        var value = FuzzValue.UInt64(high, low, shift);
        var flags = swap ? value : ~value;
        var capabilities = swap ? ~value : value;

        // Both are 64 bit values in the protocol, and the public types which carry them have 64 bit values.
        var sent = new ServerToAgent { Flags = flags, Capabilities = capabilities };
        var processor = new FrameProcessor();
        var recorder = new ServerMessageRecorder(processor);

        processor.OnServerFrame(TestSegment.Split(sent.ToByteArray(), splitSeed));

        recorder.AssertFlagsAndCapabilities(sent);
    }

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(OpAmpGenerators)])]
    public static void OnServerFrame_Rejects_A_WebSocket_Message_With_Any_Other_Header(ServerFrame frame, uint high, uint low, byte shift, int splitSeed)
    {
        var header = FuzzValue.UInt64(high, low, shift);

        if (header == 0)
        {
            return;
        }

        var processor = new FrameProcessor();
        var recorder = new ServerMessageRecorder(processor);
        var data = WithHeader(header, frame.Bytes);

        processor.OnServerFrame(TestSegment.Split(data, splitSeed), data.Length, verifyHeader: true);

        Assert.Equal(0, recorder.Count);
    }

    [Property(MaxTest = MaxTest)]
    public static void OnServerFrame_Rejects_A_WebSocket_Message_Whose_Header_Does_Not_End(byte[] data, int splitSeed)
    {
        // Every byte says that another follows, so there is never an end to the header.
        var bytes = data.Select(b => (byte)(b | 0x80)).ToArray();

        var processor = new FrameProcessor();
        var recorder = new ServerMessageRecorder(processor);

        processor.OnServerFrame(TestSegment.Split(bytes, splitSeed), bytes.Length, verifyHeader: true);

        Assert.Equal(0, recorder.Count);
    }

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(OpAmpGenerators)])]
    public static void OnServerFrame_Damaged_Message_Only_Fails_With_A_Protocol_Buffer_Exception(DamagedFrame frame)
    {
        var processor = new FrameProcessor();
        var recorder = new ServerMessageRecorder(processor);

        var exception = Record.Exception(() => processor.OnServerFrame(TestSegment.Split(frame.Bytes, frame.SplitSeed)));

        AssertOutcome(exception, recorder, frame.Bytes, frame.ToString());
    }

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(OpAmpGenerators)])]
    public static void OnServerFrame_Damaged_WebSocket_Message_Only_Fails_With_A_Protocol_Buffer_Exception(DamagedFrame frame)
    {
        var processor = new FrameProcessor();
        var recorder = new ServerMessageRecorder(processor);
        var data = WithHeader(0, frame.Bytes);

        var exception = Record.Exception(() => processor.OnServerFrame(TestSegment.Split(data, frame.SplitSeed), data.Length, verifyHeader: true));

        AssertOutcome(exception, recorder, frame.Bytes, frame.ToString());
    }

    [Property(MaxTest = MaxTest)]
    public static void OnServerFrame_Arbitrary_Bytes_Only_Fail_With_A_Protocol_Buffer_Exception(byte[] data, int splitSeed, bool webSocket)
    {
        var processor = new FrameProcessor();
        var recorder = new ServerMessageRecorder(processor);

        var exception = Record.Exception(() =>
        {
            if (webSocket)
            {
                processor.OnServerFrame(TestSegment.Split(data, splitSeed), data.Length, verifyHeader: true);
            }
            else
            {
                processor.OnServerFrame(TestSegment.Split(data, splitSeed));
            }
        });

        Assert.True(
            exception is null or InvalidProtocolBufferException,
            $"{exception?.GetType().FullName}: {exception?.Message} for {BitConverter.ToString(data)}");

        if (!webSocket && exception is null)
        {
            Assert.Equal(ServerToAgent.Parser.ParseFrom(data), Assert.Single(recorder.Envelope.Messages).Message);
        }
    }

    private static void AssertOutcome(Exception? exception, ServerMessageRecorder recorder, byte[] bytes, string description)
    {
        Assert.True(
            exception is null or InvalidProtocolBufferException,
            $"{exception?.GetType().FullName}: {exception?.Message} for {description}");

        if (exception is null)
        {
            // What was dispatched must be what a protocol buffer reader makes of the same bytes.
            recorder.AssertDispatched(ServerToAgent.Parser.ParseFrom(bytes));
        }
        else
        {
            Assert.Equal(0, recorder.Count);
        }
    }

    private static byte[] WithHeader(ulong header, byte[] data) => [.. Varint64.Encode(header), .. data];
}
