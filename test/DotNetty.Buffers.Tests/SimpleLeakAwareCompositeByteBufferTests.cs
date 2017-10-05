// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Common;
    using Xunit;

    public class SimpleLeakAwareCompositeByteBufferTests : WrappedCompositeByteBufferTests
    {
        readonly Queue<NoopResourceLeakTracker> trackers = new Queue<NoopResourceLeakTracker>();

        protected virtual Type ByteBufferType => typeof(SimpleLeakAwareByteBuffer);

        protected sealed override IByteBuffer Wrap(CompositeByteBuffer buffer)
        {
            var tracker = new NoopResourceLeakTracker();
            var leakAwareBuf = (WrappedCompositeByteBuffer)this.Wrap(buffer, tracker);
            this.trackers.Enqueue(tracker);
            return leakAwareBuf;
        }

        protected virtual IByteBuffer Wrap(CompositeByteBuffer buffer, IResourceLeakTracker tracker) => new SimpleLeakAwareCompositeByteBuffer(buffer, tracker);

        public override void Dispose()
        {
            base.Dispose();

            for (; ;)
            {
                NoopResourceLeakTracker tracker = null;
                if (this.trackers.Count > 0)
                {
                    tracker = this.trackers.Dequeue();
                }

                if (tracker == null)
                {
                    break;
                }

                Assert.True(tracker.Closed);
            }
        }

        [Fact]
        public void WrapSlice() => this.AssertWrapped(this.NewBuffer(8).Slice());

        [Fact]
        public void WrapSlice2() => this.AssertWrapped(this.NewBuffer(8).Slice(0, 1));

        [Fact]
        public void WrapReadSlice()
        {
            IByteBuffer buffer = this.NewBuffer(8);
            if (buffer.IsReadable())
            {
                this.AssertWrapped(buffer.ReadSlice(1));
            }
            else
            {
                Assert.True(buffer.Release());
            }
        }

        [Fact]
        public void WrapRetainedSlice()
        {
            IByteBuffer buffer = this.NewBuffer(8);
            this.AssertWrapped(buffer.RetainedSlice());
            Assert.True(buffer.Release());
        }

        [Fact]
        public void WrapRetainedSlice2()
        {
            IByteBuffer buffer = this.NewBuffer(8);
            if (buffer.IsReadable())
            {
                this.AssertWrapped(buffer.RetainedSlice(0, 1));
            }
            Assert.True(buffer.Release());
        }

        [Fact]
        public void WrapReadRetainedSlice()
        {
            IByteBuffer buffer = this.NewBuffer(8);
            if (buffer.IsReadable())
            {
                this.AssertWrapped(buffer.ReadRetainedSlice(1));
            }
            Assert.True(buffer.Release());
        }

        [Fact]
        public void WrapDuplicate() => this.AssertWrapped(this.NewBuffer(8).Duplicate());

        [Fact]
        public void WrapRetainedDuplicate()
        {
            IByteBuffer buffer = this.NewBuffer(8);
            this.AssertWrapped(buffer.RetainedDuplicate());
            Assert.True(buffer.Release());
        }

        protected void AssertWrapped(IByteBuffer buf)
        {
            try
            {
                Assert.IsType(this.ByteBufferType, buf);
            }
            finally
            {
                buf.Release();
            }
        }
    }
}