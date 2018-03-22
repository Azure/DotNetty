// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    public class UnpooledByteBufferAllocatorTests : AbstractByteBufferAllocatorTests
    {
        protected override IByteBufferAllocator NewAllocator(bool preferDirect) => new UnpooledByteBufferAllocator(preferDirect);

        protected override IByteBufferAllocator NewUnpooledAllocator() => new UnpooledByteBufferAllocator(false);
    }
}
