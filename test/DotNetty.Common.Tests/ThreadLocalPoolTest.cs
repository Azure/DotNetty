// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Tests
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using Xunit;

    public class ThreadLocalPoolTest
    {
        [Fact]
        public void MultipleReleaseTest()
        {
            RecyclableObject obj = RecyclableObject.NewInstance();
            Assert.True(obj.Release());
            var exception = Assert.ThrowsAny<InvalidOperationException>(() => obj.Release());
            Assert.True(exception != null);
        }

        [Fact]
        public void ReleaseTest()
        {
            RecyclableObject obj = RecyclableObject.NewInstance();
            Assert.True(obj.Release());
            RecyclableObject obj2 = RecyclableObject.NewInstance();
            Assert.Same(obj, obj2);
            Assert.True(obj2.Release());
        }

        [Fact]
        public void RecycleAtDifferentThreadTest()
        {
            RecyclableObject obj = RecyclableObject.NewInstance();

            RecyclableObject prevObject = obj;
            Task.Run(() => { Assert.True(obj.Release()); }).Wait();
            obj = RecyclableObject.NewInstance();

            Assert.True(obj == prevObject);
            Assert.True(obj.Release());
        }

        class RecyclableObject
        {
            internal static readonly ThreadLocalPool<RecyclableObject> pool =
                new ThreadLocalPool<RecyclableObject>(handle =>
                    new RecyclableObject(handle), 1, true);

            readonly ThreadLocalPool.Handle handle;

            public RecyclableObject(ThreadLocalPool.Handle handle)
            {
                this.handle = handle;
            }

            public static RecyclableObject NewInstance()
            {
                return pool.Take();
            }

            public bool Release()
            {
                return pool.Release(this, this.handle);
            }
        }

        class HandledObject
        {
            internal readonly ThreadLocalPool.Handle handle;

            internal HandledObject(ThreadLocalPool.Handle handle)
            {
                this.handle = handle;
            }

            public void Release()
            {
                this.handle.Release(this);
            }
        }

        [Fact]
        public void MaxCapacityTest()
        {
            this.MaxCapacityTest(300);
            Random rand = new Random();
            for (int i = 0; i < 50; i++)
            {
                this.MaxCapacityTest(rand.Next(1000) + 256); // 256 - 1256
            }
        }

        void MaxCapacityTest(int maxCapacity)
        {
            var recycler = new ThreadLocalPool<HandledObject>(handle => new HandledObject(handle), maxCapacity);

            var objects = new HandledObject[maxCapacity * 3];
            for (int i = 0; i < objects.Length; i++)
            {
                objects[i] = recycler.Take();
            }

            for (int i = 0; i < objects.Length; i++)
            {
                objects[i].Release();
                objects[i] = null;
            }
            Assert.Equal(maxCapacity, recycler.ThreadLocalCapacity);
        }

        [Fact]
        public void MaxCapacityWithRecycleAtDifferentThreadTest()
        {
            const int maxCapacity = 4; // Choose the number smaller than WeakOrderQueue.LINK_CAPACITY
            var recycler = new ThreadLocalPool<HandledObject>(handle => new HandledObject(handle), maxCapacity);

            // Borrow 2 * maxCapacity objects.
            // Return the half from the same thread.
            // Return the other half from the different thread.

            HandledObject[] array = new HandledObject[maxCapacity * 3];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = recycler.Take();
            }

            for (int i = 0; i < maxCapacity; i++)
            {
                array[i].Release();
            }

            Task.Run(() =>
            {
                for (int i = maxCapacity; i < array.Length; i++)
                {
                    array[i].Release();
                }
            }).Wait();

            Assert.Equal(recycler.ThreadLocalCapacity, maxCapacity);
            Assert.Equal(recycler.ThreadLocalSize, maxCapacity);

            for (int i = 0; i < array.Length; i++)
            {
                recycler.Take();
            }

            Assert.Equal(maxCapacity, recycler.ThreadLocalCapacity);
            Assert.Equal(0, recycler.ThreadLocalSize);
        }
    }
}
