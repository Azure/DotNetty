// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Groups
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IChannelGroupTaskCompletionSource : IEnumerator<Task>
    {
        IChannelGroup Group { get; }

        ChannelGroupException Cause { get; }

        Task Find(IChannel channel);

        bool IsPartialSucess();

        bool IsSucess();

        bool IsPartialFailure();
    }
}