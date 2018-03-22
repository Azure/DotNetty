// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Sockets;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    sealed class TcpChannelConfig : DefaultChannelConfiguration
    {
        readonly Dictionary<ChannelOption, int> options;

        public TcpChannelConfig(TcpChannel channel) : base(channel)
        {
            // 
            // Note:
            // Libuv automatically set SO_REUSEADDR by default on Unix but not on Windows after bind. 
            // For details:
            // https://github.com/libuv/libuv/blob/fd049399aa4ed8495928e375466970d98cb42e17/src/unix/tcp.c#L166
            // https://github.com/libuv/libuv/blob/2b32e77bb6f41e2786168ec0f32d1f0fcc78071b/src/win/tcp.c#L286
            // 
            // 

            this.options = new Dictionary<ChannelOption, int>(5);
            this.options.Add(ChannelOption.TcpNodelay, 1); // TCP_NODELAY by default
        }

        public override T GetOption<T>(ChannelOption<T> option)
        {
            if (ChannelOption.SoRcvbuf.Equals(option))
            {
                return (T)(object)this.GetReceiveBufferSize();
            }
            if (ChannelOption.SoSndbuf.Equals(option))
            {
                return (T)(object)this.GetSendBufferSize();
            }
            if (ChannelOption.TcpNodelay.Equals(option))
            {
                return (T)(object)this.GetTcpNoDelay();
            }
            if (ChannelOption.SoKeepalive.Equals(option))
            {
                return (T)(object)this.GetKeepAlive();
            }
            if (ChannelOption.SoReuseaddr.Equals(option))
            {
                return (T)(object)this.GetReuseAddress();
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
                this.SetReceiveBufferSize((int)(object)value);
            }
            else if (ChannelOption.SoSndbuf.Equals(option))
            {
                this.SetSendBufferSize((int)(object)value);
            }
            else if (ChannelOption.TcpNodelay.Equals(option))
            {
                this.SetTcpNoDelay((bool)(object)value);
            }
            else if (ChannelOption.SoKeepalive.Equals(option))
            {
                this.SetKeepAlive((bool)(object)value);
            }
            else if (ChannelOption.SoReuseaddr.Equals(option))
            {
                this.SetReuseAddress((bool)(object)value);
            }
            else
            {
                return false;
            }

            return true;
        }

        int GetReceiveBufferSize()
        {
            try
            {
                var channel = (TcpChannel)this.Channel;
                var tcp = (Tcp)channel.GetHandle();
                return tcp.ReceiveBufferSize(0);
            }
            catch (ObjectDisposedException ex)
            {
                throw new ChannelException(ex);
            }
            catch (OperationException ex)
            {
                throw new ChannelException(ex);
            }
        }

        void SetReceiveBufferSize(int value)
        {
            var channel = (TcpChannel)this.Channel;
            if (!channel.IsBound)
            {
                // Defer until bound
                if (!this.options.ContainsKey(ChannelOption.SoRcvbuf))
                {
                    this.options.Add(ChannelOption.SoRcvbuf, value);
                }
                else
                {
                    this.options[ChannelOption.SoRcvbuf] = value;
                }
            }
            else
            {
                SetReceiveBufferSize((Tcp)channel.GetHandle(), value);
            }
        }

        static void SetReceiveBufferSize(Tcp tcpHandle, int value)
        {
            try
            {
                tcpHandle.ReceiveBufferSize(value);
            }
            catch (ObjectDisposedException ex)
            {
                throw new ChannelException(ex);
            }
            catch (OperationException ex)
            {
                throw new ChannelException(ex);
            }
        }

        int GetSendBufferSize()
        {
            try
            {
                var channel = (TcpChannel)this.Channel;
                var tcp = (Tcp)channel.GetHandle();
                return tcp.SendBufferSize(0);
            }
            catch (ObjectDisposedException ex)
            {
                throw new ChannelException(ex);
            }
            catch (OperationException ex)
            {
                throw new ChannelException(ex);
            }
        }

        void SetSendBufferSize(int value)
        {
            var channel = (TcpChannel)this.Channel;
            if (!channel.IsBound)
            {
                // Defer until bound
                if (!this.options.ContainsKey(ChannelOption.SoSndbuf))
                {
                    this.options.Add(ChannelOption.SoSndbuf, value);
                }
                else
                {
                    this.options[ChannelOption.SoSndbuf] = value;
                }
            }
            else
            {
                SetSendBufferSize((Tcp)channel.GetHandle(), value);
            }
        }

        static void SetSendBufferSize(Tcp tcpHandle, int value)
        {
            try
            {
                tcpHandle.SendBufferSize(value);
            }
            catch (ObjectDisposedException ex)
            {
                throw new ChannelException(ex);
            }
            catch (OperationException ex)
            {
                throw new ChannelException(ex);
            }
        }

        bool GetTcpNoDelay()
        {
            if (this.options.TryGetValue(ChannelOption.TcpNodelay, out int value))
            {
                return value != 0;
            }
            return false;
        }

        void SetTcpNoDelay(bool value)
        {
            int optionValue = value ? 1 : 0;
            var channel = (TcpChannel)this.Channel;
            if (!channel.IsBound)
            {
                // Defer until bound
                if (!this.options.ContainsKey(ChannelOption.TcpNodelay))
                {
                    this.options.Add(ChannelOption.TcpNodelay, optionValue);
                }
                else
                {
                    this.options[ChannelOption.TcpNodelay] = optionValue;
                }
            }
            else
            {
                SetTcpNoDelay((Tcp)channel.GetHandle(), optionValue);
            }
        }

        static void SetTcpNoDelay(Tcp tcpHandle, int value)
        {
            try
            {
                tcpHandle.NoDelay(value);
            }
            catch (ObjectDisposedException ex)
            {
                throw new ChannelException(ex);
            }
            catch (OperationException ex)
            {
                throw new ChannelException(ex);
            }
        }

        bool GetKeepAlive()
        {
            if (this.options.TryGetValue(ChannelOption.SoKeepalive, out int value))
            {
                return value != 0;
            }
            return true;
        }

        void SetKeepAlive(bool value)
        {
            int optionValue = value ? 1 : 0;
            var channel = (TcpChannel)this.Channel;
            if (!channel.IsBound)
            {
                // Defer until bound
                if (!this.options.ContainsKey(ChannelOption.SoKeepalive))
                {
                    this.options.Add(ChannelOption.SoKeepalive, optionValue);
                }
                else
                {
                    this.options[ChannelOption.SoKeepalive] = optionValue;
                }
            }
            else
            {
                SetKeepAlive((Tcp)channel.GetHandle(), optionValue);
            }
        }

        static void SetKeepAlive(Tcp tcpHandle, int value)
        {
            try
            {
                tcpHandle.KeepAlive(value, 1 /* Delay in seconds to take effect*/);
            }
            catch (ObjectDisposedException ex)
            {
                throw new ChannelException(ex);
            }
            catch (OperationException ex)
            {
                throw new ChannelException(ex);
            }
        }

        bool GetReuseAddress()
        {
            try
            {
                var channel = (TcpChannel)this.Channel;
                var tcpListener = (Tcp)channel.GetHandle();
                return PlatformApi.GetReuseAddress(tcpListener);
            }
            catch (ObjectDisposedException ex)
            {
                throw new ChannelException(ex);
            }
            catch (SocketException ex)
            {
                throw new ChannelException(ex);
            }
        }

        void SetReuseAddress(bool value)
        {
            int optionValue = value ? 1 : 0;
            var channel = (TcpChannel)this.Channel;
            if (!channel.IsBound)
            {
                // Defer until registered
                if (!this.options.ContainsKey(ChannelOption.SoReuseaddr))
                {
                    this.options.Add(ChannelOption.SoReuseaddr, optionValue);
                }
                else
                {
                    this.options[ChannelOption.SoReuseaddr] = optionValue;
                }
            }
            else
            {
                SetReuseAddress((Tcp)channel.GetHandle(), optionValue);
            }
        }

        static void SetReuseAddress(Tcp tcp, int value)
        {
            try
            {
                PlatformApi.SetReuseAddress(tcp, value);
            }
            catch (ObjectDisposedException ex)
            {
                throw new ChannelException(ex);
            }
            catch (SocketException ex)
            {
                throw new ChannelException(ex);
            }
        }

        // Libuv tcp handle requires socket to be created before
        // applying options. When SetOption is called, the socket
        // is not yet created, it is deferred until channel register.
        internal void Apply()
        {
            Debug.Assert(this.options.Count <= 5);

            var channel = (TcpChannel)this.Channel;
            var tcp = (Tcp)channel.GetHandle();
            foreach (ChannelOption option in this.options.Keys)
            {
                if (ChannelOption.SoRcvbuf.Equals(option))
                {
                    SetReceiveBufferSize(tcp, this.options[ChannelOption.SoRcvbuf]);
                }
                else if (ChannelOption.SoSndbuf.Equals(option))
                {
                    SetSendBufferSize(tcp, this.options[ChannelOption.SoSndbuf]);
                }
                else if (ChannelOption.TcpNodelay.Equals(option))
                {
                    SetTcpNoDelay(tcp, this.options[ChannelOption.TcpNodelay]);
                }
                else if (ChannelOption.SoKeepalive.Equals(option))
                {
                    SetKeepAlive(tcp, this.options[ChannelOption.SoKeepalive]);
                }
                else if (ChannelOption.SoReuseaddr.Equals(option))
                {
                    SetReuseAddress(tcp, this.options[ChannelOption.SoReuseaddr]);
                }
                else
                {
                    throw new ChannelException($"Invalid channel option {option}");
                }
            }
        }
    }
}
