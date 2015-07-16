// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    /// <summary>
    /// Unpooled implementation of <see cref="IByteBufferAllocator"/>.
    /// </summary>
    public class UnpooledByteBufferAllocator : AbstractByteBufferAllocator
    {
        /// <summary>
        /// Default instance
        /// </summary>
        public static readonly UnpooledByteBufferAllocator Default = new UnpooledByteBufferAllocator();

        protected override IByteBuffer NewBuffer(int initialCapacity, int maxCapacity)
        {
            return new UnpooledHeapByteBuffer(this, initialCapacity, maxCapacity);
        }
    }
}