// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;

    public class DefaultDatagramChannelConfig : DefaultChannelConfiguration, IDatagramChannelConfig
    {
        const int DefaultFixedBufferSize = 2048;

        readonly Socket socket;

        public DefaultDatagramChannelConfig(IDatagramChannel channel, Socket socket)
            : base(channel, new FixedRecvByteBufAllocator(DefaultFixedBufferSize))
        {
            Contract.Requires(socket != null);

            this.socket = socket;
        }

        public override T GetOption<T>(ChannelOption<T> option)
        {
            if (ChannelOption.SoBroadcast.Equals(option))
            {
                return (T)(object)this.Broadcast;
            }
            if (ChannelOption.SoRcvbuf.Equals(option))
            {
                return (T)(object)this.ReceiveBufferSize;
            }
            if (ChannelOption.SoSndbuf.Equals(option))
            {
                return (T)(object)this.SendBufferSize;
            }
            if (ChannelOption.SoReuseaddr.Equals(option))
            {
                return (T)(object)this.ReuseAddress;
            }
            if (ChannelOption.IpMulticastLoopDisabled.Equals(option))
            {
                return (T)(object)this.LoopbackModeDisabled;
            }
            if (ChannelOption.IpMulticastTtl.Equals(option))
            {
                return (T)(object)this.TimeToLive;
            }
            if (ChannelOption.IpMulticastAddr.Equals(option))
            {
                return (T)(object)this.Interface;
            }
            if (ChannelOption.IpMulticastIf.Equals(option))
            {
                return (T)(object)this.NetworkInterface;
            }
            if (ChannelOption.IpTos.Equals(option))
            {
                return (T)(object)this.TrafficClass;
            }

            return base.GetOption(option);
        }

        public override bool SetOption<T>(ChannelOption<T> option, T value)
        {
            if (base.SetOption(option, value))
            {
                return true;
            }

            if (ChannelOption.SoBroadcast.Equals(option))
            {
                this.Broadcast = (bool)(object)value;
            }
            else if (ChannelOption.SoRcvbuf.Equals(option))
            {
                this.ReceiveBufferSize = (int)(object)value;
            }
            else if (ChannelOption.SoSndbuf.Equals(option))
            {
                this.SendBufferSize = (int)(object)value;
            }
            else if (ChannelOption.SoReuseaddr.Equals(option))
            {
                this.ReuseAddress = (bool)(object)value;
            }
            else if (ChannelOption.IpMulticastLoopDisabled.Equals(option))
            {
                this.LoopbackModeDisabled = (bool)(object)value;
            }
            else if (ChannelOption.IpMulticastTtl.Equals(option))
            {
                this.TimeToLive = (short)(object)value;
            }
            else if (ChannelOption.IpMulticastAddr.Equals(option))
            {
                this.Interface = (EndPoint)(object)value;
            }
            else if (ChannelOption.IpMulticastIf.Equals(option))
            {
                this.NetworkInterface = (NetworkInterface)(object)value;
            }
            else if (ChannelOption.IpTos.Equals(option))
            {
                this.TrafficClass = (int)(object)value;
            }
            else
            {
                return false;
            }

            return true;
        }

        public int SendBufferSize
        {
            get
            {
                try
                {
                    return this.socket.SendBufferSize;
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
            set
            {
                try
                {
                    this.socket.SendBufferSize = value;
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
        }

        public int ReceiveBufferSize
        {
            get
            {
                try
                {
                    return this.socket.ReceiveBufferSize;
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
            set
            {
                try
                {
                    this.socket.ReceiveBufferSize = value;
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
        }

        public int TrafficClass
        {
            get
            {
                try
                {
                    return (int)this.socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.TypeOfService);
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
            set
            {
                try
                {
                    this.socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.TypeOfService, value);
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
        }

        public bool ReuseAddress
        {
            get
            {
                try
                {
                    return (int)this.socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress) != 0;
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
            set
            {
                try
                {
                    this.socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, value ? 1 : 0);
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
        }

        public bool Broadcast
        {
            get
            {
                try
                {
                    return this.socket.EnableBroadcast;
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
            set
            {
                try
                {
                    this.socket.EnableBroadcast = value;
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
        }

        public bool LoopbackModeDisabled
        {
            get
            {
                try
                {
                    return !this.socket.MulticastLoopback;
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
            set
            {
                try
                {
                    this.socket.MulticastLoopback = !value;
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
        }

        public short TimeToLive
        {
            get
            {
                try
                {
                    return (short)this.socket.GetSocketOption(
                        this.AddressFamilyOptionLevel,
                        SocketOptionName.MulticastTimeToLive);
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
            set
            {
                try
                {
                    this.socket.SetSocketOption(
                        this.AddressFamilyOptionLevel,
                        SocketOptionName.MulticastTimeToLive,
                        value);
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
        }

        public EndPoint Interface
        {
            get
            {
                try
                {
                    return this.socket.LocalEndPoint;
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
            set
            {
                Contract.Requires(value != null);

                try
                {
                    this.socket.Bind(value);
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
        }

        public NetworkInterface NetworkInterface
        {
            get
            {
                try
                {
                    NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
                    int value = (int)this.socket.GetSocketOption(
                        this.AddressFamilyOptionLevel,
                        SocketOptionName.MulticastInterface);
                    int index = IPAddress.NetworkToHostOrder(value);

                    if (interfaces.Length > 0
                        && index >= 0
                        && index < interfaces.Length)
                    {
                        return interfaces[index];
                    }

                    return null;
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
            set
            {
                Contract.Requires(value != null);

                try
                {
                    int index = this.GetNetworkInterfaceIndex(value);
                    if (index >= 0)
                    {
                        this.socket.SetSocketOption(
                            this.AddressFamilyOptionLevel,
                            SocketOptionName.MulticastInterface,
                            index);
                    }
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
        }

        internal SocketOptionLevel AddressFamilyOptionLevel
        {
            get
            {
                if (this.socket.AddressFamily == AddressFamily.InterNetwork)
                {
                    return SocketOptionLevel.IP;
                }

                if (this.socket.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    return SocketOptionLevel.IPv6;
                }

                throw new NotSupportedException($"Socket address family {this.socket.AddressFamily} not supported, expecting InterNetwork or InterNetworkV6");
            }
        }

        internal int GetNetworkInterfaceIndex(NetworkInterface networkInterface)
        {
            Contract.Requires(networkInterface != null);

            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            for (int index = 0; index < interfaces.Length; index++)
            {
                if (interfaces[index].Id == networkInterface.Id)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}