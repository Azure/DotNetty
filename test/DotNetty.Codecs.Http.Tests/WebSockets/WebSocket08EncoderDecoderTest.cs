// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using System;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class WebSocket08EncoderDecoderTest : IDisposable
    {
        readonly IByteBuffer binTestData;
        readonly string strTestData;

        public WebSocket08EncoderDecoderTest()
        {
            const int MaxTestdataLength = 100 * 1024;

            this.binTestData = Unpooled.Buffer(MaxTestdataLength);
            byte j = 0;
            for (int i = 0; i < MaxTestdataLength; i++)
            {
                this.binTestData.Array[i] = j;
                j++;
            }

            var s = new StringBuilder();
            char c = 'A';
            for (int i = 0; i < MaxTestdataLength; i++)
            {
                s.Append(c);
                c++;
                if (c == 'Z')
                {
                    c = 'A';
                }
            }
            this.strTestData = s.ToString();
        }

        [Fact]
        public void WebSocketEncodingAndDecoding()
        {
            // Test without masking
            var outChannel = new EmbeddedChannel(new WebSocket08FrameEncoder(false));
            var inChannel = new EmbeddedChannel(new WebSocket08FrameDecoder(false, false, 1024 * 1024, false));
            this.ExecuteTests(outChannel, inChannel);

            // Test with activated masking
            outChannel = new EmbeddedChannel(new WebSocket08FrameEncoder(true));
            inChannel = new EmbeddedChannel(new WebSocket08FrameDecoder(true, false, 1024 * 1024, false));
            this.ExecuteTests(outChannel, inChannel);

            // Test with activated masking and an unmasked expecting but forgiving decoder
            outChannel = new EmbeddedChannel(new WebSocket08FrameEncoder(true));
            inChannel = new EmbeddedChannel(new WebSocket08FrameDecoder(false, false, 1024 * 1024, true));
            this.ExecuteTests(outChannel, inChannel);
        }

        void ExecuteTests(EmbeddedChannel outChannel, EmbeddedChannel inChannel)
        {
            // Test at the boundaries of each message type, because this shifts the position of the mask field
            // Test min. 4 lengths to check for problems related to an uneven frame length
            this.ExecuteTests(outChannel, inChannel, 0);
            this.ExecuteTests(outChannel, inChannel, 1);
            this.ExecuteTests(outChannel, inChannel, 2);
            this.ExecuteTests(outChannel, inChannel, 3);
            this.ExecuteTests(outChannel, inChannel, 4);
            this.ExecuteTests(outChannel, inChannel, 5);

            this.ExecuteTests(outChannel, inChannel, 125);
            this.ExecuteTests(outChannel, inChannel, 126);
            this.ExecuteTests(outChannel, inChannel, 127);
            this.ExecuteTests(outChannel, inChannel, 128);
            this.ExecuteTests(outChannel, inChannel, 129);

            this.ExecuteTests(outChannel, inChannel, 65535);
            this.ExecuteTests(outChannel, inChannel, 65536);
            this.ExecuteTests(outChannel, inChannel, 65537);
            this.ExecuteTests(outChannel, inChannel, 65538);
            this.ExecuteTests(outChannel, inChannel, 65539);
        }

        void ExecuteTests(EmbeddedChannel outChannel, EmbeddedChannel inChannel, int testDataLength)
        {
            this.TextWithLen(outChannel, inChannel, testDataLength);
            this.BinaryWithLen(outChannel, inChannel, testDataLength);
        }

        void TextWithLen(EmbeddedChannel outChannel, EmbeddedChannel inChannel, int testDataLength)
        {
            string testStr = this.strTestData.Substring(0, testDataLength);
            outChannel.WriteOutbound(new TextWebSocketFrame(testStr));

            // Transfer encoded data into decoder
            // Loop because there might be multiple frames (gathering write)
            while (true)
            {
                var encoded = outChannel.ReadOutbound<IByteBuffer>();
                if (encoded != null)
                {
                    inChannel.WriteInbound(encoded);
                }
                else
                {
                    break;
                }
            }

            var txt = inChannel.ReadInbound<TextWebSocketFrame>();
            Assert.NotNull(txt);
            Assert.Equal(testStr, txt.Text());
            txt.Release();
        }

        void BinaryWithLen(EmbeddedChannel outChannel, EmbeddedChannel inChannel, int testDataLength)
        {
            this.binTestData.Retain(); // need to retain for sending and still keeping it
            this.binTestData.SetIndex(0, testDataLength); // Send only len bytes
            outChannel.WriteOutbound(new BinaryWebSocketFrame(this.binTestData));

            // Transfer encoded data into decoder
            // Loop because there might be multiple frames (gathering write)
            while (true)
            {
                var encoded = outChannel.ReadOutbound<IByteBuffer>();
                if (encoded != null)
                {
                    inChannel.WriteInbound(encoded);
                }
                else
                {
                    break;
                }
            }

            var binFrame = inChannel.ReadInbound<BinaryWebSocketFrame>();
            Assert.NotNull(binFrame);
            int readable = binFrame.Content.ReadableBytes;
            Assert.Equal(readable, testDataLength);
            for (int i = 0; i < testDataLength; i++)
            {
                Assert.Equal(this.binTestData.GetByte(i), binFrame.Content.GetByte(i));
            }

            binFrame.Release();
        }

        public void Dispose()
        {
            this.binTestData.SafeRelease();
        }
    }
}
