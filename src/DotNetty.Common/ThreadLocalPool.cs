// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Threading;

    public class ThreadLocalPool
    {
        public class Handle
        {
            internal object Value;
            internal readonly Stack Stack;

            internal Handle(Stack stack)
            {
                this.Stack = stack;
            }

            public bool Release<T>(T value)
                where T : class
            {
                Contract.Requires(value == this.Value, "value differs from one backed by this handle.");

                Stack stack = this.Stack;
                if (stack.Thread != Thread.CurrentThread)
                {
                    return false;
                }

                if (stack.Count == stack.Owner.MaxCapacity)
                {
                    return false;
                }

                stack.Push(this);
                return true;
            }
        }

        internal class Stack : Stack<Handle>
        {
            public readonly ThreadLocalPool Owner;
            public readonly Thread Thread;

            public Stack(int initialCapacity, ThreadLocalPool owner, Thread thread)
                : base(initialCapacity)
            {
                this.Owner = owner;
                this.Thread = thread;
            }
        }

        internal static readonly int DefaultMaxCapacity = 262144;
        internal static readonly int InitialCapacity = Math.Min(256, DefaultMaxCapacity);

        public ThreadLocalPool(int maxCapacity)
        {
            Contract.Requires(maxCapacity > 0);
            this.MaxCapacity = maxCapacity;
        }

        public int MaxCapacity { get; private set; }
    }

    public sealed class ThreadLocalPool<T> : ThreadLocalPool
        where T : class
    {
        readonly ThreadLocal<Stack> threadLocal;
        readonly Func<Handle, T> valueFactory;
        readonly bool preCreate;

        public ThreadLocalPool(Func<Handle, T> valueFactory)
            : this(valueFactory, DefaultMaxCapacity)
        {
        }

        public ThreadLocalPool(Func<Handle, T> valueFactory, int maxCapacity)
            : this(valueFactory, maxCapacity, false)
        {
        }

        public ThreadLocalPool(Func<Handle, T> valueFactory, int maxCapacity, bool preCreate)
            : base(maxCapacity)
        {
            Contract.Requires(valueFactory != null);

            this.preCreate = preCreate;

#if TRACE
            this.threadLocal = new ThreadLocal<Stack>(this.InitializeStorage, true);
#else
            this.threadLocal = new ThreadLocal<ThreadLocalPool.Stack>(this.InitializeStorage);
#endif
            this.valueFactory = valueFactory;
        }

        Stack InitializeStorage()
        {
            var stack = new Stack(InitialCapacity, this, Thread.CurrentThread);
            if (this.preCreate)
            {
                for (int i = 0; i < this.MaxCapacity; i++)
                {
                    stack.Push(this.CreateValue(stack));
                }
            }
            return stack;
        }

        [Conditional("TRACE")]
        public void LogUsage(string context)
        {
            // todo: perf counter or log
            int bufferCountInStacks = 0;
            foreach (Stack x in this.threadLocal.Values)
            {
                bufferCountInStacks += x.Count;
            }
            Console.WriteLine(context + ": " + bufferCountInStacks);
        }

        public T Take()
        {
            Stack stack = this.threadLocal.Value;
            Handle handle = stack.Count == 0 ? this.CreateValue(stack) : stack.Pop();
            return (T)handle.Value;
        }

        Handle CreateValue(Stack stack)
        {
            var handle = new Handle(stack);
            T value = this.valueFactory(handle);
            handle.Value = value;
            return handle;
        }
    }
}