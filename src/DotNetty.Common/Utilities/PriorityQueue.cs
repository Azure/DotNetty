// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;

    public class PriorityQueue<T> : IEnumerable<T>
        where T : class
    {
        readonly IComparer<T> comparer;
        int count;
        int capacity;
        T[] items;

        public PriorityQueue(IComparer<T> comparer)
        {
            Contract.Requires(comparer != null);

            this.comparer = comparer;
            this.capacity = 11;
            this.items = new T[this.capacity];
        }

        public PriorityQueue()
            : this(Comparer<T>.Default)
        {
        }

        public int Count => this.count;

        public T Dequeue()
        {
            T result = this.Peek();
            if (result == null)
            {
                return null;
            }

            int newCount = --this.count;
            T lastItem = this.items[newCount];
            this.items[newCount] = null;
            if (newCount > 0)
            {
                this.TrickleDown(0, lastItem);
            }

            return result;
        }

        public T Peek() => this.count == 0 ? null : this.items[0];

        public void Enqueue(T item)
        {
            Contract.Requires(item != null);

            int oldCount = this.count;
            if (oldCount == this.capacity)
            {
                this.GrowHeap();
            }
            this.count = oldCount + 1;
            this.BubbleUp(oldCount, item);
        }

        public void Remove(T item)
        {
            int index = Array.IndexOf(this.items, item);
            if (index == -1)
            {
                return;
            }

            this.count--;
            if (index == this.count)
            {
                this.items[index] = default(T);
            }
            else
            {
                T last = this.items[this.count];
                this.items[this.count] = default(T);
                this.TrickleDown(index, last);
                if (this.items[index] == last)
                {
                    this.BubbleUp(index, last);
                }
            }
        }

        void BubbleUp(int index, T item)
        {
            // index > 0 means there is a parent
            while (index > 0)
            {
                int parentIndex = (index - 1) >> 1;
                T parentItem = this.items[parentIndex];
                if (this.comparer.Compare(item, parentItem) >= 0)
                {
                    break;
                }
                this.items[index] = parentItem;
                index = parentIndex;
            }
            this.items[index] = item;
        }

        void GrowHeap()
        {
            int oldCapacity = this.capacity;
            this.capacity = oldCapacity + (oldCapacity <= 64 ? oldCapacity + 2 : (oldCapacity >> 1));
            var newHeap = new T[this.capacity];
            Array.Copy(this.items, 0, newHeap, 0, this.count);
            this.items = newHeap;
        }

        void TrickleDown(int index, T item)
        {
            int middleIndex = this.count >> 1;
            while (index < middleIndex)
            {
                int childIndex = (index << 1) + 1;
                T childItem = this.items[childIndex];
                int rightChildIndex = childIndex + 1;
                if (rightChildIndex < this.count
                    && this.comparer.Compare(childItem, this.items[rightChildIndex]) > 0)
                {
                    childIndex = rightChildIndex;
                    childItem = this.items[rightChildIndex];
                }
                if (this.comparer.Compare(item, childItem) <= 0)
                {
                    break;
                }
                this.items[index] = childItem;
                index = childIndex;
            }
            this.items[index] = item;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < this.count; i++)
            {
                yield return this.items[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public void Clear()
        {
            this.count = 0;
            Array.Clear(this.items, 0, 0);
        }
    }
}