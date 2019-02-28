// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;

    /// <inheritdoc />
    /// <summary>
    ///     Exception thrown during instances where a reference count is used incorrectly
    /// </summary>
    public class IllegalReferenceCountException : InvalidOperationException
    {
        public IllegalReferenceCountException(int count)
            : base($"Illegal reference count of {count} for this object")
        {
        }

        public IllegalReferenceCountException(int refCnt, int increment)
            : base("refCnt: " + refCnt + ", " + (increment > 0 ? "increment: " + increment : "decrement: " + -increment))
        {
        }
    }
}
