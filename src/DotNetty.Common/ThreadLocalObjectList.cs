// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common
{
    using System.Collections.Generic;

    public class ThreadLocalObjectList : List<object>
    {
        const int DefaultInitialCapacity = 8;

        static readonly ThreadLocalPool<ThreadLocalObjectList> Pool = new ThreadLocalPool<ThreadLocalObjectList>(handle => new ThreadLocalObjectList(handle));

        readonly ThreadLocalPool.Handle returnHandle;

        ThreadLocalObjectList(ThreadLocalPool.Handle returnHandle)
        {
            this.returnHandle = returnHandle;
        }

        public static ThreadLocalObjectList NewInstance() => NewInstance(DefaultInitialCapacity);

        public static ThreadLocalObjectList NewInstance(int minCapacity)
        {
            ThreadLocalObjectList ret = Pool.Take();
            if (ret.Capacity < minCapacity)
            {
                ret.Capacity = minCapacity;
            }
            return ret;

        }

        public void Return()
        {
            this.Clear();
            this.returnHandle.Release(this);
        }
    }
}