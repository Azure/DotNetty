// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System.Diagnostics.Contracts;
    using System.Threading;

    /// <summary>
    ///     Default <see cref="IAttributeMap" /> implementation which use simple synchronization per bucket to keep the memory
    ///     overhead
    ///     as low as possible.
    /// </summary>
    public class DefaultAttributeMap : IAttributeMap
    {
        const int BucketSize = 4;
        const int Mask = BucketSize - 1;

        // Initialize lazily to reduce memory consumption; updated by AtomicReferenceFieldUpdater above.
        volatile DefaultAttribute[] attributes;

        public IAttribute<T> GetAttribute<T>(AttributeKey<T> key)
            where T : class
        {
            Contract.Requires(key != null);

            DefaultAttribute[] attrs = this.attributes;
            if (attrs == null)
            {
                attrs = new DefaultAttribute[BucketSize];
                // Not using ConcurrentHashMap due to high memory consumption.
                attrs = Interlocked.CompareExchange(ref this.attributes, attrs, null) ?? attrs;
            }

            int i = Index(key);
            DefaultAttribute head = Volatile.Read(ref attrs[i]);
            if (head == null)
            {
                // No head exists yet which means we may be able to add the attribute without synchronization and just
                // use compare and set. At worst we need to fallback to synchronization
                head = new DefaultAttribute<T>(key);

                if (Interlocked.CompareExchange(ref this.attributes[i], head, null) == null)
                {
                    // we were able to add it so return the head right away
                    return (IAttribute<T>)head;
                }

                head = Volatile.Read(ref attrs[i]);
            }

            lock (head)
            {
                DefaultAttribute curr = head;
                while (true)
                {
                    if (!curr.Removed && curr.GetKey() == key)
                    {
                        return (IAttribute<T>)curr;
                    }

                    DefaultAttribute next = curr.Next;
                    if (next == null)
                    {
                        var attr = new DefaultAttribute<T>(head, key);
                        curr.Next = attr;
                        attr.Prev = curr;
                        return attr;
                    }
                    else
                    {
                        curr = next;
                    }
                }
            }
        }

        public bool HasAttribute<T>(AttributeKey<T> key)
            where T : class
        {
            Contract.Requires(key != null);

            DefaultAttribute[] attrs = this.attributes;
            if (attrs == null)
            {
                // no attribute exists
                return false;
            }

            int i = Index(key);
            DefaultAttribute head = Volatile.Read(ref attrs[i]);
            if (head == null)
            {
                // No attribute exists which point to the bucket in which the head should be located
                return false;
            }

            // check on the head can be done without synchronization
            if (head.GetKey() == key && !head.Removed)
            {
                return true;
            }

            lock (head)
            {
                // we need to synchronize on the head
                DefaultAttribute curr = head.Next;
                while (curr != null)
                {
                    if (!curr.Removed && curr.GetKey() == key)
                    {
                        return true;
                    }
                    curr = curr.Next;
                }
                return false;
            }
        }

        static int Index<T>(AttributeKey<T> key) => key.Id & Mask;

        abstract class DefaultAttribute
        {
            // The head of the linked-list this attribute belongs to, which may be itself
            protected readonly DefaultAttribute Head;

            // Double-linked list to prev and next node to allow fast removal
            public DefaultAttribute Prev;
            public DefaultAttribute Next;

            // Will be set to true one the attribute is removed via GetAndRemove() or Remove()
            public volatile bool Removed;

            public abstract IConstant GetKey();

            protected DefaultAttribute()
            {
                this.Head = this;
            }

            protected DefaultAttribute(DefaultAttribute head)
            {
                this.Head = head;
            }
        }

        sealed class DefaultAttribute<T> : DefaultAttribute, IAttribute<T>
            where T : class
        {
            readonly AttributeKey<T> key;
            T value;

            public DefaultAttribute(DefaultAttribute head, AttributeKey<T> key)
                : base(head)
            {
                this.key = key;
            }

            public DefaultAttribute(AttributeKey<T> key)
            {
                this.key = key;
            }

            public AttributeKey<T> Key => this.key;

            public T Get() => Volatile.Read(ref this.value);

            public void Set(T value) => Volatile.Write(ref this.value, value);

            public T GetAndSet(T value) => Interlocked.Exchange(ref this.value, value);

            public T SetIfAbsent(T value)
            {
                while (!this.CompareAndSet(null, value))
                {
                    T old = this.Get();
                    if (old != null)
                    {
                        return old;
                    }
                }
                return default(T);
            }

            public T GetAndRemove()
            {
                this.Removed = true;
                T oldValue = this.GetAndSet(null);
                this.Remove0();
                return oldValue;
            }

            public bool CompareAndSet(T oldValue, T newValue) => Interlocked.CompareExchange(ref this.value, newValue, oldValue) == oldValue;

            public void Remove()
            {
                this.Removed = true;
                this.Set(null);
                this.Remove0();
            }

            void Remove0()
            {
                lock (this.Head)
                {
                    // We only update the linked-list structure if prev != null because if it is null this
                    // DefaultAttribute acts also as head. The head must never be removed completely and just be
                    // marked as removed as all synchronization is done on the head itself for each bucket.
                    // The head itself will be GC'ed once the DefaultAttributeMap is GC'ed. So at most 5 heads will
                    // be removed lazy as the array size is 5.
                    if (this.Prev != null)
                    {
                        this.Prev.Next = this.Next;

                        if (this.Next != null)
                        {
                            this.Next.Prev = this.Prev;
                        }

                        // Null out prev and next - this will guard against multiple remove0() calls which may corrupt
                        // the linked list for the bucket.
                        this.Prev = null;
                        this.Next = null;
                    }
                }
            }

            public override IConstant GetKey() => this.key;
        }
    }
}