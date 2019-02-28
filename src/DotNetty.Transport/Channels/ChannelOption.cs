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

        /// <summary>
        /// Returns the <see cref="ChannelOption"/> of the specified name.
        /// </summary>
        /// <typeparam name="T">The type of option being retrieved.</typeparam>
        /// <param name="name">The name of the desired option.</param>
        /// <returns>The matching <see cref="ChannelOption{T}"/> instance.</returns>
        public static ChannelOption<T> ValueOf<T>(string name) => (ChannelOption<T>)Pool.ValueOf<T>(name);

        /// <summary>
        /// Returns the <see cref="ChannelOption{T}"/> of the given pair: (<see cref="Type"/>, secondary name)
        /// </summary>
        /// <typeparam name="T">The type of option being retrieved.</typeparam>
        /// <param name="firstNameComponent">
        /// A <see cref="Type"/> whose name will be used as the first part of the desired option's name.
        /// </param>
        /// <param name="secondNameComponent">
        /// A string representing the second part of the desired option's name.
        /// </param>
        /// <returns>The matching <see cref="ChannelOption{T}"/> instance.</returns>
        public static ChannelOption<T> ValueOf<T>(Type firstNameComponent, string secondNameComponent) => (ChannelOption<T>)Pool.ValueOf<T>(firstNameComponent, secondNameComponent);

        /// <summary>
        /// Checks whether a given <see cref="ChannelOption"/> exists.
        /// </summary>
        /// <param name="name">The name of the <see cref="ChannelOption"/>.</param>
        /// <returns><c>true</c> if a <see cref="ChannelOption"/> exists for the given <paramref name="name"/>, otherwise <c>false</c>.</returns>
        public static bool Exists(string name) => Pool.Exists(name);

        /// <summary>
        /// Creates a new <see cref="ChannelOption"/> for the given <paramref name="name"/>.
        /// </summary>
        /// <typeparam name="T">The type of option to create.</typeparam>
        /// <param name="name">The name to associate with the new option.</param>
        /// <exception cref="ArgumentException">Thrown if a <see cref="ChannelOption"/> for the given <paramref name="name"/> exists.</exception>
        /// <returns>The new <see cref="ChannelOption{T}"/> instance.</returns>
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