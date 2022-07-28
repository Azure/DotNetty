// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Embedded
{
    public interface IEmbeddedChannel : IChannel
    {
        bool WriteInbound(params object[] msgs);

        bool WriteOutbound(params object[] msgs);

        T ReadInbound<T>();

        T ReadOutbound<T>(); 

        bool Finish();
    }
}