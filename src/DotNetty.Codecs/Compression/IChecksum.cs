// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Compression
{
    interface IChecksum
    {
        void Update(byte[] buf, int index, int len);

        void Reset();

        void Reset(long init);

        long GetValue();

        IChecksum Copy();
    }
}
