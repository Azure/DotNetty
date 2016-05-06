// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System.Diagnostics.Contracts;
    using System.Net.Sockets;
    using DotNetty.Common.Utilities;

    public class SocketChannelAsyncOperation : SocketAsyncEventArgs
    {
        public SocketChannelAsyncOperation(AbstractSocketChannel channel)
            : this(channel, true)
        {
        }

        public SocketChannelAsyncOperation(AbstractSocketChannel channel, bool setEmptyBuffer)
        {
            Contract.Requires(channel != null);

            this.Channel = channel;
            this.Completed += AbstractSocketChannel.IoCompletedCallback;
            if (setEmptyBuffer)
            {
                this.SetBuffer(ArrayExtensions.ZeroBytes, 0, 0);
            }
        }

        public void Validate()
        {
            SocketError socketError = this.SocketError;
            if (socketError != SocketError.Success)
            {
                throw new SocketException((int)socketError);
            }
        }

        public AbstractSocketChannel Channel { get; private set; }
    }
}