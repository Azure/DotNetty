// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System.Collections.Generic;

    public interface ICharSequence : IReadOnlyList<char>
    {
        /// Start is the inclusive start index to begin the subsequence.
        /// End is the exclusive end index to end the subsequence.
        ICharSequence SubSequence(int start, int end);

        ICharSequence SubSequence(int start);

        int IndexOf(char ch, int start = 0);

        bool RegionMatches(int thisStart, ICharSequence seq, int start, int length);

        bool RegionMatchesIgnoreCase(int thisStart, ICharSequence seq, int start, int length);

        bool ContentEquals(ICharSequence other);

        bool ContentEqualsIgnoreCase(ICharSequence other);

        int HashCode(bool ignoreCase);

        string ToString(int start);

        string ToString();
    }
}
