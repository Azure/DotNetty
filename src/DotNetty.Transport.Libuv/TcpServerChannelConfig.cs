// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    sealed class TcpServerChannelConfig : DefaultChannelConfiguration
    {
        const int DefaultBacklog = 128;

        public TcpServerChannelConfig(TcpServerChannel channel) : base(channel)
        {
        }

        public override T GetOption<T>(ChannelOption<T> option)
        {
            if (ChannelOption.SoRcvbuf.Equals(option))
            {
                return (T)(object)this.ReceiveBufferSize;
            }
            if (ChannelOption.SoReuseaddr.Equals(option))
            {
                return (T)(object)this.ReuseAddress;
            }
            if (ChannelOption.SoBacklog.Equals(option))
            {
                return (T)(object)this.Backlog;
            }

            return base.GetOption(option);
        }

        public override bool SetOption<T>(ChannelOption<T> option, T value)
        {
            this.Validate(option, value);

            if (ChannelOption.SoRcvbuf.Equals(option))
            {
                this.ReceiveBufferSize = (int)(object)value;
            }
            else if (ChannelOption.SoReuseaddr.Equals(option))
            {
                this.ReuseAddress = (bool)(object)value;
            }
            else if (ChannelOption.SoBacklog.Equals(option))
            {
                this.Backlog = (int)(object)value;
            }
            else
            {
                return base.SetOption(option, value);
            }

            return true;
        }

        public int ReceiveBufferSize { get; private set; }

        public bool ReuseAddress { get; private set; }

        public int Backlog { get; private set; } = DefaultBacklog;

        internal void SetOptions(TcpListener tcpListener)
        {
            if (this.ReceiveBufferSize > 0)
            {
                tcpListener.ReceiveBufferSize(this.ReceiveBufferSize);
            }

            if (this.ReuseAddress)
            {
                tcpListener.SimultaneousAccepts(this.ReuseAddress);
            }
        }
    }
}
