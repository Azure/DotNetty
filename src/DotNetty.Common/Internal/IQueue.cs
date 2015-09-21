// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    public interface IQueue<T>
    {
        bool TryEnqueue(T element);

        T Dequeue();

        T Peek();

        int Count { get; }

        bool IsEmpty { get; }

        void Clear();
    }
}