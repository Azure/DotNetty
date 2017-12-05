// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable SuggestBaseTypeForParameter
namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Sockets;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    using TcpListener = Native.TcpListener;

    sealed class TcpServerChannelConfig : DefaultChannelConfiguration
    {
        const int DefaultBacklog = 200;
        int backlog;

        readonly Dictionary<ChannelOption, int> options;

        public TcpServerChannelConfig(TcpServerChannel channel) : base(channel)
        {
            this.options = new Dictionary<ChannelOption, int>(3);

            // 
            // Note:
            // Libuv automatically set SO_REUSEADDR by default on Unix but not on Windows after bind. 
            // For details:
            // https://github.com/libuv/libuv/blob/fd049399aa4ed8495928e375466970d98cb42e17/src/unix/tcp.c#L166
            // https://github.com/libuv/libuv/blob/2b32e77bb6f41e2786168ec0f32d1f0fcc78071b/src/win/tcp.c#L286
            // 
            // 
            this.backlog = DefaultBacklog;
        }

        public override T GetOption<T>(ChannelOption<T> option)
        {
            if (ChannelOption.SoRcvbuf.Equals(option))
            {
                return (T)(object)this.GetReceiveBufferSize();
            }
            if (ChannelOption.SoReuseaddr.Equals(option))
            {
                return (T)(object)this.GetReuseAddress();
            }
            if (ChannelOption.SoReuseport.Equals(option))
            {
                return (T)(object)this.GetReusePort();
            }
            if (ChannelOption.SoBacklog.Equals(option))
            {
                return (T)(object)this.backlog;
            }

            return base.GetOption(option);
        }

        public override bool SetOption<T>(ChannelOption<T> option, T value)
        {
            this.Validate(option, value);

            if (ChannelOption.SoRcvbuf.Equals(option))
            {
                this.SetReceiveBufferSize((int)(object)value);
            }
            else if (ChannelOption.SoReuseaddr.Equals(option))
            {
                this.SetReuseAddress((bool)(object)value);
            }
            else if (ChannelOption.SoReuseport.Equals(option))
            {
                this.SetReusePort((bool)(object)value);
            }
            else if (ChannelOption.SoBacklog.Equals(option))
            {
                this.backlog = (int)(object)value;
            }
            else
            {
                return base.SetOption(option, value);
            }

            return true;
        }

        public int Backlog => this.backlog;

        int GetReceiveBufferSize()
        {
            try
            {
                var channel = (TcpServerChannel)this.Channel;
                var tcpListener = (TcpListener)channel.GetHandle();
                return tcpListener.ReceiveBufferSize(0);
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
            var channel = (TcpServerChannel)this.Channel;
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
                SetReceiveBufferSize((TcpHandle)channel.GetHandle(), value);
            }
        }

        static void SetReceiveBufferSize(TcpHandle tcpHandle, int value)
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

        bool GetReuseAddress()
        {
            try
            {
                var channel = (TcpServerChannel)this.Channel;
                var tcpListener = (TcpListener)channel.GetHandle();
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
            var channel = (TcpServerChannel)this.Channel;
            if (!channel.IsBound)
            {
                // Defer until bound
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
                SetReuseAddress((TcpListener)channel.GetHandle(), optionValue);
            }
        }

        static void SetReuseAddress(TcpListener listener, int value)
        {
            try
            {
                PlatformApi.SetReuseAddress(listener, value);
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

        bool GetReusePort()
        {
            try
            {
                var channel = (TcpServerChannel)this.Channel;
                var tcpListener = (TcpListener)channel.GetHandle();
                return PlatformApi.GetReusePort(tcpListener);
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

        void SetReusePort(bool value)
        {
            int optionValue = value ? 1 : 0;
            var channel = (TcpServerChannel)this.Channel;
            if (!channel.IsBound)
            {
                // Defer until bound
                if (!this.options.ContainsKey(ChannelOption.SoReuseport))
                {
                    this.options.Add(ChannelOption.SoReuseport, optionValue);
                }
                else
                {
                    this.options[ChannelOption.SoReuseport] = optionValue;
                }
            }
            else
            {
                SetReusePort((TcpListener)channel.GetHandle(), optionValue);
            }
        }

        static void SetReusePort(TcpListener listener, int value)
        {
            try
            {
                PlatformApi.SetReusePort(listener, value);
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
        // is not yet created, it is deferred until channel socket
        // is bound.
        internal void Apply()
        {
            Debug.Assert(this.options.Count <= 3);

            var channel = (TcpServerChannel)this.Channel;
            var tcpListener = (TcpListener)channel.GetHandle();
            foreach (ChannelOption option in this.options.Keys)
            {
                if (ChannelOption.SoRcvbuf.Equals(option))
                {
                    SetReceiveBufferSize(tcpListener, this.options[ChannelOption.SoRcvbuf]);
                }
                else if (ChannelOption.SoReuseaddr.Equals(option))
                {
                    SetReuseAddress(tcpListener, this.options[ChannelOption.SoReuseaddr]);
                }
                else if (ChannelOption.SoReuseport.Equals(option))
                {
                    SetReusePort(tcpListener, this.options[ChannelOption.SoReuseport]);
                }
                else
                {
                    throw new ChannelException($"Invalid channel option {option}");
                }
            }
        }
    }
}
