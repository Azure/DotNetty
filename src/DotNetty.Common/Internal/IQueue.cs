// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    public interface IQueue<T>
    {
        bool TryEnqueue(T item);

        bool TryDequeue(out T item);

        bool TryPeek(out T item);

        int Count { get; }

        bool IsEmpty { get; }

        void Clear();
    }
}