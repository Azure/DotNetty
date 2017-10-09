// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System.Threading;
    using DotNetty.Common;

    sealed class NoopResourceLeakTracker : IResourceLeakTracker
    {
        long value;

        public bool Closed => this.value == 1;

        public void Record()
        {
            // NOOP
        }

        public void Record(object hint)
        {
            // NOOP
        }

        public bool Close(object trackedObject)
        {
            return Interlocked.CompareExchange(ref this.value, 1, 0) == 0;
        }
    }
}
