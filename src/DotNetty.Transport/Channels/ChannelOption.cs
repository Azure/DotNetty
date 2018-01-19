// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Net.NetworkInformation;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    public abstract class ChannelOption : AbstractConstant<ChannelOption>
    {
        class ChannelOptionPool : ConstantPool
        {
            protected override IConstant NewConstant<T>(int id, string name) => new ChannelOption<T>(id, name);
        }

        static readonly ChannelOptionPool Pool = new ChannelOptionPool();

        /// <summary>Returns the {@link ChannelOption} of the specified name.</summary>
        public static ChannelOption<T> ValueOf<T>(string name) => (ChannelOption<T>)Pool.ValueOf<T>(name);

        /// <summary>Shortcut of {@link #valueOf(String) valueOf(firstNameComponent.getName() + "#" + secondNameComponent)}.</summary>
        public static ChannelOption<T> ValueOf<T>(Type firstNameComponent, string secondNameComponent) => (ChannelOption<T>)Pool.ValueOf<T>(firstNameComponent, secondNameComponent);

        /// <summary>Returns {@code true} if a {@link ChannelOption} exists for the given {@code name}.</summary>
        public static bool Exists(string name) => Pool.Exists(name);

        /// <summary>Creates a new {@link ChannelOption} for the given {@code name} or fail with an
        /// {@link IllegalArgumentException} if a {@link ChannelOption} for the given {@code name} exists.
        /// </summary>
        public static  ChannelOption<T> NewInstance<T>(string name) => (ChannelOption<T>)Pool.NewInstance<T>(name);

        public static readonly ChannelOption<IByteBufferAllocator> Allocator = ValueOf<IByteBufferAllocator>("ALLOCATOR");
        public static readonly ChannelOption<IRecvByteBufAllocator> RcvbufAllocator = ValueOf<IRecvByteBufAllocator>("RCVBUF_ALLOCATOR");
        public static readonly ChannelOption<IMessageSizeEstimator> MessageSizeEstimator = ValueOf<IMessageSizeEstimator>("MESSAGE_SIZE_ESTIMATOR");

        public static readonly ChannelOption<TimeSpan> ConnectTimeout = ValueOf<TimeSpan>("CONNECT_TIMEOUT");
        public static readonly ChannelOption<int> WriteSpinCount = ValueOf<int>("WRITE_SPIN_COUNT");
        public static readonly ChannelOption<int> WriteBufferHighWaterMark = ValueOf<int>("WRITE_BUFFER_HIGH_WATER_MARK");
        public static readonly ChannelOption<int> WriteBufferLowWaterMark = ValueOf<int>("WRITE_BUFFER_LOW_WATER_MARK");

        public static readonly ChannelOption<bool> AllowHalfClosure = ValueOf<bool>("ALLOW_HALF_CLOSURE");
        public static readonly ChannelOption<bool> AutoRead = ValueOf<bool>("AUTO_READ");

        public static readonly ChannelOption<bool> SoBroadcast = ValueOf<bool>("SO_BROADCAST");
        public static readonly ChannelOption<bool> SoKeepalive = ValueOf<bool>("SO_KEEPALIVE");
        public static readonly ChannelOption<int> SoSndbuf = ValueOf<int>("SO_SNDBUF");
        public static readonly ChannelOption<int> SoRcvbuf = ValueOf<int>("SO_RCVBUF");
        public static readonly ChannelOption<bool> SoReuseaddr = ValueOf<bool>("SO_REUSEADDR");
        public static readonly ChannelOption<bool> SoReuseport = ValueOf<bool>("SO_REUSEPORT");
        public static readonly ChannelOption<int> SoLinger = ValueOf<int>("SO_LINGER");
        public static readonly ChannelOption<int> SoBacklog = ValueOf<int>("SO_BACKLOG");
        public static readonly ChannelOption<int> SoTimeout = ValueOf<int>("SO_TIMEOUT");

        public static readonly ChannelOption<int> IpTos = ValueOf<int>("IP_TOS");
        public static readonly ChannelOption<EndPoint> IpMulticastAddr = ValueOf<EndPoint>("IP_MULTICAST_ADDR");
        public static readonly ChannelOption<NetworkInterface> IpMulticastIf = ValueOf<NetworkInterface>("IP_MULTICAST_IF");
        public static readonly ChannelOption<int> IpMulticastTtl = ValueOf<int>("IP_MULTICAST_TTL");
        public static readonly ChannelOption<bool> IpMulticastLoopDisabled = ValueOf<bool>("IP_MULTICAST_LOOP_DISABLED");

        public static readonly ChannelOption<bool> TcpNodelay = ValueOf<bool>("TCP_NODELAY");

        internal ChannelOption(int id, string name)
            : base(id, name)
        {
        }

        public abstract bool Set(IChannelConfiguration configuration, object value);
    }

    public sealed class ChannelOption<T> : ChannelOption
    {
        internal ChannelOption(int id, string name)
            : base(id, name)
        {
        }

        public void Validate(T value) => Contract.Requires(value != null);

        public override bool Set(IChannelConfiguration configuration, object value) => configuration.SetOption(this, (T)value);
    }
}