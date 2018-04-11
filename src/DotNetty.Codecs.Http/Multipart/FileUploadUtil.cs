// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Multipart
{
    using System;

    static class FileUploadUtil
    {
        public static int HashCode(IFileUpload upload) => upload.Name.GetHashCode();

        public static bool Equals(IFileUpload upload1, IFileUpload upload2) => 
            upload1.Name.Equals(upload2.Name, StringComparison.OrdinalIgnoreCase);

        public static int CompareTo(IFileUpload upload1, IFileUpload upload2) => 
            string.Compare(upload1.Name, upload2.Name, StringComparison.OrdinalIgnoreCase);
    }
}
