// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Multipart
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Common.Utilities;

    sealed class CaseIgnoringComparator : IEqualityComparer<ICharSequence>, IComparer<ICharSequence>
    {
        public static readonly IEqualityComparer<ICharSequence> Default = new CaseIgnoringComparator();

        CaseIgnoringComparator()
        {
        }

        public int Compare(ICharSequence x, ICharSequence y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }
            if (x == null)
            {
                return -1;
            }
            if (y == null)
            {
                return 1;
            }

            int o1Length = x.Count;
            int o2Length = y.Count;
            int min = Math.Min(o1Length, o2Length);
            for (int i = 0; i < min; i++)
            {
                char c1 = x[i];
                char c2 = y[i];
                if (c1 != c2)
                {
                    c1 = char.ToUpper(c1);
                    c2 = char.ToUpper(c2);
                    if (c1 != c2)
                    {
                        c1 = char.ToLower(c1);
                        c2 = char.ToLower(c2);
                        if (c1 != c2)
                        {
                            return c1 - c2;
                        }
                    }
                }
            }

            return o1Length - o2Length;
        }

        public bool Equals(ICharSequence x, ICharSequence y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }
            
            if (x == null || y == null)
            {
                return false;
            }

            int o1Length = x.Count;
            int o2Length = y.Count;

            if (o1Length != o2Length)
            {
                return false;
            }

            for (int i = 0; i < o1Length; i++)
            {
                char c1 = x[i];
                char c2 = y[i];
                if (c1 != c2)
                {
                    c1 = char.ToUpper(c1);
                    c2 = char.ToUpper(c2);
                    if (c1 != c2)
                    {
                        c1 = char.ToLower(c1);
                        c2 = char.ToLower(c2);
                        if (c1 != c2)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        public int GetHashCode(ICharSequence obj) => obj.HashCode(true);
    }
}
