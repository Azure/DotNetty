// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.Multipart
{
    using DotNetty.Codecs.Http.Multipart;
    using Xunit;

    public sealed class DiskFileUploadTest
    {
        [Fact]
        public void DiskFileUploadEquals()
        {
            var f2 = new DiskFileUpload("d1", "d1", "application/json", null, null, 100);
            Assert.Equal(f2, f2);
        }
    }
}
