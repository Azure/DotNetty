// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.Multipart
{
    using DotNetty.Codecs.Http.Multipart;
    using Xunit;

    public sealed class MemoryFileUploadTest
    {
        [Fact]
        public void MemoryFileUploadEquals()
        {
            var f1 = new MemoryFileUpload("m1", "m1", "application/json", null, null, 100);
            Assert.Equal(f1, f1);
        }
    }
}
