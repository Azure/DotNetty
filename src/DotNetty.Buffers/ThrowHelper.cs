// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;

    static class ThrowHelper
    {
        public static void ThrowIndexOutOfRangeException(string message) => throw new IndexOutOfRangeException(message);

        public static void ThrowIllegalReferenceCountException(int count = 0) => throw new IllegalReferenceCountException(count);
    }
}
