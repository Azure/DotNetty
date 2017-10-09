// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    public interface IByteBufferAllocatorMetricProvider
    {
        /// <summary>
        /// Returns a <see cref="IByteBufferAllocatorMetric"/> for a <see cref="IByteBufferAllocator"/>
        /// </summary>
        IByteBufferAllocatorMetric Metric { get; }
    }
}
