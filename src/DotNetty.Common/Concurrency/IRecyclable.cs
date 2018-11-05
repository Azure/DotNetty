// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    public interface IRecyclable
    {
        void Init(IEventExecutor executor);
        
        void Recycle();
    }
}