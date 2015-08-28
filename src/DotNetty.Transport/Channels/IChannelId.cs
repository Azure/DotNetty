﻿
using System;

namespace DotNetty.Transport.Channels
{
    public interface IChannelId : IComparable<IChannelId>
    {
        string AsShortText { get; }

        string AsLongText { get; }
    }
}
