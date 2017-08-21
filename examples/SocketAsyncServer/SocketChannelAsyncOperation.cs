using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net.Sockets;
using System.Text;

namespace SocketAsyncServer
{
    public class SocketChannelAsyncOperation : SocketAsyncEventArgs
    {
        public static readonly byte[] ZeroBytes = new byte[0];

        public SocketChannelAsyncOperation(SocketChannel channel)
            : this(channel, true)
        {
        }

        public SocketChannelAsyncOperation(SocketChannel channel, bool setEmptyBuffer)
        {
            Contract.Requires(channel != null);

            this.Channel = channel;
            this.Completed += SocketChannel.IoCompletedCallback;
            if (setEmptyBuffer)
            {
                this.SetBuffer(ZeroBytes, 0, 0);
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

        public SocketChannel Channel { get; private set; }
    }
}
