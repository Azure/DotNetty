// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using DotNetty.Common;

    /// <summary>
    ///     Abstract base class for <see cref="IByteBufferAllocator" /> instances
    /// </summary>
    public abstract class AbstractByteBufferAllocator : IByteBufferAllocator
    {
        public const int DefaultInitialCapacity = 256;
        public const int DefaultMaxComponents = 16;
        public const int DefaultMaxCapacity = int.MaxValue;

        protected static IByteBuffer ToLeakAwareBuffer(IByteBuffer buf)
        {
            IResourceLeak leak;
            switch (ResourceLeakDetector.Level)
            {
                case ResourceLeakDetector.DetectionLevel.Simple:
                    leak = AbstractByteBuffer.LeakDetector.Open(buf);
                    if (leak != null)
                    {
                        buf = new SimpleLeakAwareByteBuffer(buf, leak);
                    }
                    break;
                case ResourceLeakDetector.DetectionLevel.Advanced:
                case ResourceLeakDetector.DetectionLevel.Paranoid:
                    leak = AbstractByteBuffer.LeakDetector.Open(buf);
                    if (leak != null)
                    {
                        buf = new AdvancedLeakAwareByteBuffer(buf, leak);
                    }
                    break;
                case ResourceLeakDetector.DetectionLevel.Disabled:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return buf;
        }

        readonly IByteBuffer emptyBuffer;

        protected AbstractByteBufferAllocator()
        {
            this.emptyBuffer = new EmptyByteBuffer(this);
        }

        public IByteBuffer Buffer() => this.Buffer(DefaultInitialCapacity, int.MaxValue);

        public IByteBuffer Buffer(int initialCapacity) => this.Buffer(initialCapacity, int.MaxValue);

        public IByteBuffer Buffer(int initialCapacity, int maxCapacity)
        {
            if (initialCapacity == 0 && maxCapacity == 0)
            {
                return this.emptyBuffer;
            }

            Validate(initialCapacity, maxCapacity);

            return this.NewBuffer(initialCapacity, maxCapacity);
        }

        public CompositeByteBuffer CompositeBuffer() => this.CompositeBuffer(DefaultMaxComponents);

        public CompositeByteBuffer CompositeBuffer(int maxComponents) => new CompositeByteBuffer(this, maxComponents);

        protected abstract IByteBuffer NewBuffer(int initialCapacity, int maxCapacity);

        #region Range validation

        static void Validate(int initialCapacity, int maxCapacity)
        {
            if (initialCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "initialCapacity must be greater than zero");
            }

            if (initialCapacity > maxCapacity)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), $"initialCapacity ({initialCapacity}) must be greater than maxCapacity ({maxCapacity})");
            }
        }

        #endregion
    }
}