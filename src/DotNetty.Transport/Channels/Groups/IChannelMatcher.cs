// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Groups
{
    public interface IChannelMatcher
    {
        bool Matches(IChannel channel);
    }
}