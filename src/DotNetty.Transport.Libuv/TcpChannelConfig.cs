// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    sealed class TcpChannelConfig : DefaultChannelConfiguration
    {
        public TcpChannelConfig(TcpChannel channel) : base(channel)
        {
        }

        public override T GetOption<T>(ChannelOption<T> option)
        {
            if (ChannelOption.SoRcvbuf.Equals(option))
            {
                return (T)(object)this.ReceiveBufferSize;
            }
            if (ChannelOption.SoSndbuf.Equals(option))
            {
                return (T)(object)this.SendBufferSize;
            }
            if (ChannelOption.TcpNodelay.Equals(option))
            {
                return (T)(object)this.TcpNoDelay;
            }
            if (ChannelOption.SoKeepalive.Equals(option))
            {
                return (T)(object)this.KeepAlive;
            }
            if (ChannelOption.SoReuseaddr.Equals(option))
            {
                return (T)(object)this.ReuseAddress;
            }

            return base.GetOption(option);
        }

        public override bool SetOption<T>(ChannelOption<T> option, T value)
        {
            if (base.SetOption(option, value))
            {
                return true;
            }

            if (ChannelOption.SoRcvbuf.Equals(option))
            {
                this.ReceiveBufferSize = (int)(object)value;
            }
            else if (ChannelOption.SoSndbuf.Equals(option))
            {
                this.SendBufferSize = (int)(object)value;
            }
            else if (ChannelOption.TcpNodelay.Equals(option))
            {
                this.TcpNoDelay = (bool)(object)value;
            }
            else if (ChannelOption.SoKeepalive.Equals(option))
            {
                this.KeepAlive = (bool)(object)value;
            }
            else if (ChannelOption.SoReuseaddr.Equals(option))
            {
                this.ReuseAddress = (bool)(object)value;
            }
            else
            {
                return false;
            }

            return true;
        }

        public int ReceiveBufferSize { get; private set; }

        public int SendBufferSize { get; private set; }

        public bool TcpNoDelay { get; private set; } = true;

        public bool KeepAlive { get; private set; }

        public bool ReuseAddress { get; private set; }

        internal void SetOptions(Tcp tcp)
        {
            if (this.TcpNoDelay)
            {
                tcp.NoDelay(this.TcpNoDelay);
            }
            if (this.ReceiveBufferSize > 0)
            {
                tcp.ReceiveBufferSize(this.ReceiveBufferSize);
            }
            if (this.SendBufferSize > 0)
            {
                tcp.SendBufferSize(this.SendBufferSize);
            }
            if (this.KeepAlive)
            {
                tcp.KeepAlive(true, 1 /* Delay in seconds */);
            }
        }
    }
}
