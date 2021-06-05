// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Tests
{
    using DotNetty.Common.Concurrency;
    using System;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class ThreadLocalPoolTest
    {
        private static ThreadLocalPool<HandledObject> NewPool(int max)
            => new ThreadLocalPool<HandledObject>(handle => new HandledObject(handle), max);

        //Not use Task to do all `AtDifferentThreadTest`, because can't promise Task won't be run inline.

        [Fact]
        public void ThreadCanBeCollectedEvenIfHandledObjectIsReferencedTest()
        {
            ThreadLocalPool<HandledObject> pool = NewPool(1024);
            HandledObject reference = null;
            WeakReference<Thread> threadRef = null;
            WeakReference<XThread> xThreadRef = null;

            var thread1 = new Thread(() =>
            {
                //Don't know the reason, but thread2 will not be collected without wrapped with thread1
                var thread2 = new Thread(() =>
                {
                    Volatile.Write(ref xThreadRef, new WeakReference<XThread>(XThread.CurrentThread));
                    HandledObject data = pool.Take();
                    // Store a reference to the HandledObject to ensure it is not collected when the run method finish.
                    Volatile.Write(ref reference, data);
                });
                Volatile.Write(ref threadRef, new WeakReference<Thread>(thread2));
                thread2.Start();
                thread2.Join();
                Assert.True(Volatile.Read(ref threadRef)?.TryGetTarget(out _));
                Assert.True(Volatile.Read(ref xThreadRef)?.TryGetTarget(out _));

                GC.KeepAlive(thread2);
                // Null out so it can be collected.
                thread2 = null;
            });
            thread1.Start();
            thread1.Join();

            for (int i = 0; i < 5; ++i)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();

                if (Volatile.Read(ref threadRef)?.TryGetTarget(out _) == true || Volatile.Read(ref xThreadRef)?.TryGetTarget(out _) == true)
                    Thread.Sleep(100);
            }

            Assert.False(Volatile.Read(ref threadRef)?.TryGetTarget(out _));
            Assert.False(Volatile.Read(ref xThreadRef)?.TryGetTarget(out _));

            // Now call recycle after the Thread was collected to ensure this still works...
            reference.Release();
            reference = null;
        }

        [Fact]
        public void MultipleReleaseTest()
        {
            ThreadLocalPool<HandledObject> pool = NewPool(1024);
            HandledObject obj = pool.Take();
            obj.Release();
            var exception = Assert.ThrowsAny<InvalidOperationException>(() => obj.Release());
            Assert.True(exception != null);
        }
        [Fact]
        public void MultipleReleaseAtDifferentThreadTest()
        {
            ThreadLocalPool<HandledObject> pool = NewPool(1024);
            HandledObject obj = pool.Take();

            Thread thread = new Thread(() =>
            {
                obj.Release();
            });
            thread.Start();
            thread.Join();

            ExceptionDispatchInfo exceptionDispatchInfo = null;
            Thread thread2 = new Thread(() =>
            {
                try
                {
                    obj.Release();
                }
                catch (Exception ex)
                {
                    Volatile.Write(ref exceptionDispatchInfo, ExceptionDispatchInfo.Capture(ex));
                }
            });
            thread2.Start();
            thread2.Join();
            var exception = Assert.ThrowsAny<InvalidOperationException>(() => Volatile.Read(ref exceptionDispatchInfo)?.Throw());
            Assert.True(exception != null);
        }

        [Fact]
        public void ReleaseTest()
        {
            ThreadLocalPool<HandledObject> pool = NewPool(1024);
            HandledObject obj = pool.Take();
            obj.Release();
            HandledObject obj2 = pool.Take();
            Assert.Same(obj, obj2);
            obj2.Release();
        }

        [Fact]
        public void ReleaseDisableTest()
        {
            ThreadLocalPool<HandledObject> pool = NewPool(-1);
            HandledObject obj = pool.Take();
            obj.Release();
            HandledObject obj2 = pool.Take();
            Assert.NotSame(obj, obj2);
            obj2.Release();
        }

        class HandledObject
        {
            internal readonly ThreadLocalPool.Handle handle;

            internal HandledObject(ThreadLocalPool.Handle handle)
            {
                this.handle = handle;
            }

            public void Release() => this.handle.Release(this);
        }

        [Fact]
        public void MaxCapacityTest()
        {
            MaxCapacityTest0(300);
            var rand = new Random();
            for (int i = 0; i < 50; i++)
            {
                MaxCapacityTest0(rand.Next(1000) + 256); // 256 - 1256
            }
        }

        static void MaxCapacityTest0(int maxCapacity)
        {
            var recycler = NewPool(maxCapacity);

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
            Assert.True(maxCapacity >= recycler.ThreadLocalCapacity,
                "The threadLocalCapacity (" + recycler.ThreadLocalCapacity + ") must be <= maxCapacity ("
                + maxCapacity + ") as we not pool all new handles internally");
        }

        [Fact]
        public void ReleaseAtDifferentThreadTest()
        {
            ThreadLocalPool<HandledObject> pool = new ThreadLocalPool<HandledObject>(handle => new HandledObject(handle),
                256, 10, 2, 10);

            HandledObject obj = pool.Take();
            HandledObject obj2 = pool.Take();
            Thread thread = new Thread(() =>
            {
                obj.Release();
                obj2.Release();
            });
            thread.Start();
            thread.Join();

            Assert.Same(pool.Take(), obj);
            Assert.NotSame(pool.Take(), obj2);
        }

        [Fact]
        public void MaxCapacityWithReleaseAtDifferentThreadTest()
        {
            const int maxCapacity = 4; // Choose the number smaller than WeakOrderQueue.LINK_CAPACITY
            var pool = NewPool(maxCapacity);

            // Borrow 2 * maxCapacity objects.
            // Return the half from the same thread.
            // Return the other half from the different thread.

            var array = new HandledObject[maxCapacity * 3];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = pool.Take();
            }

            for (int i = 0; i < maxCapacity; i++)
            {
                array[i].Release();
            }

            Thread thread = new Thread(() =>
            {
                for (int i = maxCapacity; i < array.Length; i++)
                {
                    array[i].Release();
                }
            });
            thread.Start();
            thread.Join();

            Assert.Equal(maxCapacity, pool.ThreadLocalCapacity);
            Assert.Equal(1, pool.ThreadLocalSize);

            for (int i = 0; i < array.Length; i++)
            {
                pool.Take();
            }

            Assert.Equal(maxCapacity, pool.ThreadLocalCapacity);
            Assert.Equal(0, pool.ThreadLocalSize);
        }

        [Fact]
        public void DiscardingExceedingElementsWithReleaseAtDifferentThreadTest()
        {
            int maxCapacity = 32;
            int instancesCount = 0;

            ThreadLocalPool<HandledObject> pool = new ThreadLocalPool<HandledObject>(handle =>
            {
                Interlocked.Increment(ref instancesCount);
                return new HandledObject(handle);
            }, maxCapacity, 2);

            // Borrow 2 * maxCapacity objects.
            HandledObject[] array = new HandledObject[maxCapacity * 2];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = pool.Take();
            }

            Assert.Equal(array.Length, Volatile.Read(ref instancesCount));
            // Reset counter.
            Volatile.Write(ref instancesCount, 0);

            // Release from other thread.
            Thread thread = new Thread(() =>
            {
                for (int i = 0; i < array.Length; i++)
                {
                    array[i].Release();
                }
            });
            thread.Start();
            thread.Join();

            Assert.Equal(0, Volatile.Read(ref instancesCount));

            // Borrow 2 * maxCapacity objects. Half of them should come from
            // the recycler queue, the other half should be freshly allocated.
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = pool.Take();
            }

            // The implementation uses maxCapacity / 2 as limit per WeakOrderQueue
            Assert.True(array.Length - maxCapacity / 2 <= Volatile.Read(ref instancesCount),
                "The instances count (" + Volatile.Read(ref instancesCount) + ") must be <= array.length (" + array.Length
                + ") - maxCapacity (" + maxCapacity + ") / 2 as we not pool all new handles" +
                " internally");
        }
    }
}