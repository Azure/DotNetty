// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;

    public sealed class DefaultMessageSizeEstimator : IMessageSizeEstimator
    {
        sealed class HandleImpl : IMessageSizeEstimatorHandle
        {
            readonly int unknownSize;

            public HandleImpl(int unknownSize)
            {
                this.unknownSize = unknownSize;
            }

            public int Size(object msg)
            {
                if (msg is IByteBuffer)
                {
                    return ((IByteBuffer)msg).ReadableBytes;
                }
                if (msg is IByteBufferHolder)
                {
                    return ((IByteBufferHolder)msg).Content.ReadableBytes;
                }
                // todo: FileRegion support
                //if (msg instanceof FileRegion) {
                //    return 0;
                //}
                return this.unknownSize;
            }
        }

        /// <summary>
        ///     Return the default implementation which returns {@code -1} for unknown messages.
        /// </summary>
        public static readonly IMessageSizeEstimator Default = new DefaultMessageSizeEstimator(0);

        readonly IMessageSizeEstimatorHandle handle;

        /// <summary>
        ///     Create a new instance
        ///     @param unknownSize       The size which is returned for unknown messages.
        /// </summary>
        public DefaultMessageSizeEstimator(int unknownSize)
        {
            Contract.Requires(unknownSize >= 0);
            this.handle = new HandleImpl(unknownSize);
        }

        public IMessageSizeEstimatorHandle NewHandle() => this.handle;
    }
}