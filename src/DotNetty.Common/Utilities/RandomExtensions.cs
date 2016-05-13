// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;

    public static class RandomExtensions
    {
        public static long NextLong(this Random random) => random.Next() << 32 & unchecked((uint)random.Next());
    }
}