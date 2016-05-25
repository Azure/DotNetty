// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using DotNetty.Buffers;

    public interface IChannelConfiguration
    {
        T GetOption<T>(ChannelOption<T> option);

        bool SetOption(ChannelOption option, object value);

        bool SetOption<T>(ChannelOption<T> option, T value);

        //void SetOptions<T>(Dictionary<ChannelOption<T>, object> option);

        TimeSpan ConnectTimeout { get; set; }

        int WriteSpinCount { get; set; }

        IByteBufferAllocator Allocator { get; set; }

        IRecvByteBufAllocator RecvByteBufAllocator { get; set; }

        bool AutoRead { get; set; }

        int WriteBufferHighWaterMark { get; set; }

        int WriteBufferLowWaterMark { get; set; }

        IMessageSizeEstimator MessageSizeEstimator { get; set; }
    }
}