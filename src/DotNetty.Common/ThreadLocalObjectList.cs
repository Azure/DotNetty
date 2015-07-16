// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common
{
    using System.Collections.Generic;

    public class ThreadLocalObjectList : List<object>
    {
        static readonly ThreadLocalPool<ThreadLocalObjectList> Pool = new ThreadLocalPool<ThreadLocalObjectList>(handle => new ThreadLocalObjectList(handle));

        readonly ThreadLocalPool.Handle returnHandle;

        ThreadLocalObjectList(ThreadLocalPool.Handle returnHandle)
        {
            this.returnHandle = returnHandle;
        }

        public static ThreadLocalObjectList Take()
        {
            return Pool.Take();
        }

        public void Return()
        {
            this.Clear();
            this.returnHandle.Release(this);
        }
    }
}