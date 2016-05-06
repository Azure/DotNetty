// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    public abstract class AbstractQueue<T> : IQueue<T>
    {
        public abstract bool TryEnqueue(T element);

        public abstract T Dequeue();

        public abstract T Peek();

        public abstract int Count { get; }

        public abstract bool IsEmpty { get; }

        public abstract void Clear();
    }
}