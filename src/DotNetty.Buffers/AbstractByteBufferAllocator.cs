// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Runtime.CompilerServices;
    using DotNetty.Common;

    /// <inheritdoc />
    /// <summary>
    ///     Abstract base class for <see cref="T:DotNetty.Buffers.IByteBufferAllocator" /> instances
    /// </summary>
    public abstract class AbstractByteBufferAllocator : IByteBufferAllocator
    {
        public const int DefaultInitialCapacity = 256;
        public const int DefaultMaxComponents = 16;
        public const int DefaultMaxCapacity = int.MaxValue;
        const int CalculateThreshold = 1048576 * 4; // 4 MiB page

        protected static IByteBuffer ToLeakAwareBuffer(IByteBuffer buf)
        {
            IResourceLeakTracker leak;
            switch (ResourceLeakDetector.Level)
            {
                case ResourceLeakDetector.DetectionLevel.Simple:
                    leak = AbstractByteBuffer.LeakDetector.Track(buf);
                    if (leak != null)
                    {
                        buf = new SimpleLeakAwareByteBuffer(buf, leak);
                    }
                    break;
                case ResourceLeakDetector.DetectionLevel.Advanced:
                case ResourceLeakDetector.DetectionLevel.Paranoid:
                    leak = AbstractByteBuffer.LeakDetector.Track(buf);
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

        protected static CompositeByteBuffer ToLeakAwareBuffer(CompositeByteBuffer buf)
        {
            IResourceLeakTracker leak;
            switch (ResourceLeakDetector.Level)
            {
                case ResourceLeakDetector.DetectionLevel.Simple:
                    leak = AbstractByteBuffer.LeakDetector.Track(buf);
                    if (leak != null)
                    {
                        buf = new SimpleLeakAwareCompositeByteBuffer(buf, leak);
                    }
                    break;
                case ResourceLeakDetector.DetectionLevel.Advanced:
                case ResourceLeakDetector.DetectionLevel.Paranoid:
                    leak = AbstractByteBuffer.LeakDetector.Track(buf);
                    if (leak != null)
                    {
                        buf = new AdvancedLeakAwareCompositeByteBuffer(buf, leak);
                    }
                    break;
                case ResourceLeakDetector.DetectionLevel.Disabled:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return buf;
        }

        readonly bool directByDefault;
        readonly IByteBuffer emptyBuffer;

        protected AbstractByteBufferAllocator()
        {
            this.emptyBuffer = new EmptyByteBuffer(this);
        }

        protected AbstractByteBufferAllocator(bool preferDirect)
        {
            this.directByDefault = preferDirect;
            this.emptyBuffer = new EmptyByteBuffer(this);
        }

        public IByteBuffer Buffer() => this.directByDefault ? this.DirectBuffer() : this.HeapBuffer();

        public IByteBuffer Buffer(int initialCapacity) => 
            this.directByDefault ? this.DirectBuffer(initialCapacity) : this.HeapBuffer(initialCapacity);

        public IByteBuffer Buffer(int initialCapacity, int maxCapacity) => 
            this.directByDefault ? this.DirectBuffer(initialCapacity, maxCapacity) : this.HeapBuffer(initialCapacity, maxCapacity);

        public IByteBuffer HeapBuffer() => this.HeapBuffer(DefaultInitialCapacity, DefaultMaxCapacity);

        public IByteBuffer HeapBuffer(int initialCapacity) => this.HeapBuffer(initialCapacity, DefaultMaxCapacity);
        
        public IByteBuffer HeapBuffer(int initialCapacity, int maxCapacity)
        {
            if (initialCapacity == 0 && maxCapacity == 0)
            {
                return this.emptyBuffer;
            }

            Validate(initialCapacity, maxCapacity);
            return this.NewHeapBuffer(initialCapacity, maxCapacity);
        }

        public IByteBuffer DirectBuffer() => this.DirectBuffer(DefaultInitialCapacity, DefaultMaxCapacity);

        public IByteBuffer DirectBuffer(int initialCapacity) => this.DirectBuffer(initialCapacity, DefaultMaxCapacity);

        public IByteBuffer DirectBuffer(int initialCapacity, int maxCapacity)
        {
            if (initialCapacity == 0 && maxCapacity == 0)
            {
                return this.emptyBuffer;
            }
            Validate(initialCapacity, maxCapacity);
            return this.NewDirectBuffer(initialCapacity, maxCapacity);
        }

        public CompositeByteBuffer CompositeBuffer() => 
            this.directByDefault ? this.CompositeDirectBuffer() : this.CompositeHeapBuffer();

        public CompositeByteBuffer CompositeBuffer(int maxComponents) => 
            this.directByDefault ? this.CompositeDirectBuffer(maxComponents) : this.CompositeHeapBuffer(maxComponents);

        public CompositeByteBuffer CompositeHeapBuffer() => this.CompositeHeapBuffer(DefaultMaxComponents);

        public virtual CompositeByteBuffer CompositeHeapBuffer(int maxNumComponents) => 
            ToLeakAwareBuffer(new CompositeByteBuffer(this, false, maxNumComponents));

        public CompositeByteBuffer CompositeDirectBuffer() => this.CompositeDirectBuffer(DefaultMaxComponents);

        public virtual CompositeByteBuffer CompositeDirectBuffer(int maxNumComponents) => 
            ToLeakAwareBuffer(new CompositeByteBuffer(this, true, maxNumComponents));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Validate(int initialCapacity, int maxCapacity)
        {
            if (initialCapacity < 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(initialCapacity), "initialCapacity must be greater than zero");
            }

            if (initialCapacity > maxCapacity)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(initialCapacity), $"initialCapacity ({initialCapacity}) must be greater than maxCapacity ({maxCapacity})");
            }
        }

        protected abstract IByteBuffer NewHeapBuffer(int initialCapacity, int maxCapacity);

        protected abstract IByteBuffer NewDirectBuffer(int initialCapacity, int maxCapacity);

        public abstract bool IsDirectBufferPooled { get; }

        public int CalculateNewCapacity(int minNewCapacity, int maxCapacity)
        {
            if (minNewCapacity < 0)
            {
                throw new ArgumentOutOfRangeException($"minNewCapacity: {minNewCapacity} (expected: 0+)");
            }
            if (minNewCapacity > maxCapacity)
            {
                throw new ArgumentOutOfRangeException($"minNewCapacity: {minNewCapacity} (expected: not greater than maxCapacity({maxCapacity})");
            }

            const int Threshold = CalculateThreshold; // 4 MiB page
            if (minNewCapacity == CalculateThreshold)
            {
                return Threshold;
            }

            int newCapacity;
            // If over threshold, do not double but just increase by threshold.
            if (minNewCapacity > Threshold)
            {
                newCapacity = minNewCapacity / Threshold * Threshold;
                if (newCapacity > maxCapacity - Threshold)
                {
                    newCapacity = maxCapacity;
                }
                else
                {
                    newCapacity += Threshold;
                }

                return newCapacity;
            }

            // Not over threshold. Double up to 4 MiB, starting from 64.
            newCapacity = 64;
            while (newCapacity < minNewCapacity)
            {
                newCapacity <<= 1;
            }

            return Math.Min(newCapacity, maxCapacity);
        }
    }
}