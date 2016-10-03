// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Tests.Common
{
    using System;
    using System.Collections.Generic;

    public static class EnumerableExtensions
    {
        public static IEnumerable<IEnumerable<T>> Split<T>(this IEnumerable<T> src, Func<T, bool> splitFunc)
        {
            var group = new List<T>();
            foreach (T elem in src)
            {
                if (splitFunc(elem))
                {
                    yield return group;
                    group = new List<T>();
                }
                else
                {
                    group.Add(elem);
                }
            }
            yield return group;
        }
    }
}