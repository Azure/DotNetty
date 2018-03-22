// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tests
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels.Embedded;

    class BatchingWriteStrategy : IWriteStrategy
    {
        readonly int maxBatchSize;
        readonly TimeSpan timeWindow;
        readonly bool forceSizing;
        IByteBuffer pendingBuffer;
        EmbeddedChannel channel;

        public BatchingWriteStrategy(int maxBatchSize, TimeSpan timeWindow, bool forceSizing)
        {
            this.maxBatchSize = maxBatchSize;
            this.timeWindow = timeWindow;
            this.forceSizing = forceSizing;
        }

        public async Task WriteToChannelAsync(EmbeddedChannel ch, ArraySegment<byte> input)
        {
            this.channel = ch;

            if (input.Count == 0)
            {
                if (this.pendingBuffer != null)
                {
                    this.FlushPendingBuffer();
                }
                ch.WriteInbound(Unpooled.Empty);
            }

            if (this.pendingBuffer == null)
            {
                await this.SetupPendingBufferAsync(input);
                return;
            }

            if (input.Count + this.pendingBuffer.ReadableBytes >= this.maxBatchSize)
            {
                if (this.forceSizing)
                {
                    int appendLength = this.maxBatchSize - this.pendingBuffer.ReadableBytes;
                    this.pendingBuffer.WriteBytes(input.Array, input.Offset, appendLength);
                    this.FlushPendingBuffer();
                    if (input.Count - appendLength > 0)
                    {
                        await this.SetupPendingBufferAsync(new ArraySegment<byte>(input.Array, input.Offset + appendLength, input.Count - appendLength));
                    }
                }
                else
                {
                    this.FlushPendingBuffer();
                    await this.SetupPendingBufferAsync(input);
                }
            }
            else
            {
                this.pendingBuffer.WriteBytes(input.Array, input.Offset, input.Count);
            }
        }

        async Task SetupPendingBufferAsync(ArraySegment<byte> input)
        {
            this.pendingBuffer = Unpooled.Buffer(Math.Max(this.maxBatchSize, input.Count));
            this.pendingBuffer.WriteBytes(input.Array, input.Offset, input.Count);

            if (this.pendingBuffer.ReadableBytes >= this.maxBatchSize)
            {
                if (this.forceSizing)
                {
                    do
                    {
                        this.channel.WriteInbound(this.pendingBuffer.ReadBytes(this.maxBatchSize));
                    }
                    while (this.pendingBuffer.ReadableBytes >= this.maxBatchSize);
                    if (!this.pendingBuffer.IsReadable())
                    {
                        this.pendingBuffer = null;
                    }
                    else
                    {
                        await this.ScheduleFlushByTimeoutAsync();
                    }
                }
                else
                {
                    this.FlushPendingBuffer();
                }
            }
            else
            {
                await this.ScheduleFlushByTimeoutAsync();
            }
        }

        void FlushPendingBuffer()
        {
            this.channel.WriteInbound(this.pendingBuffer);
            this.pendingBuffer = null;
        }

        async Task ScheduleFlushByTimeoutAsync()
        {
            IByteBuffer buffer = this.pendingBuffer;
            await Task.Delay(this.timeWindow);
            if (ReferenceEquals(this.pendingBuffer, buffer))
            {
                this.FlushPendingBuffer();
            }
        }

        public override string ToString() => $"batch({this.maxBatchSize}, {this.timeWindow}, {this.forceSizing})";
    }
}