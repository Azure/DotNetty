// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace DotNetty.Codecs.Http.WebSockets
{
    using System.Runtime.CompilerServices;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    sealed class Utf8Validator : IByteProcessor
    {
        const int Utf8Accept = 0;
        const int Utf8Reject = 12;

        static readonly byte[] Types = {  0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 7, 7, 7, 7,
            7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 8,
            8, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            2, 2, 10, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 4, 3, 3, 11, 6, 6, 6, 5, 8, 8, 8, 8, 8,
            8, 8, 8, 8, 8, 8 };

        static readonly byte[] States = { 0, 12, 24, 36, 60, 96, 84, 12, 12, 12, 48, 72, 12, 12,
            12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 0, 12, 12, 12, 12, 12, 0, 12, 0, 12, 12,
            12, 24, 12, 12, 12, 12, 12, 24, 12, 24, 12, 12, 12, 12, 12, 12, 12, 12, 12, 24, 12, 12,
            12, 12, 12, 24, 12, 12, 12, 12, 12, 12, 12, 24, 12, 12, 12, 12, 12, 12, 12, 12, 12, 36,
            12, 36, 12, 12, 12, 36, 12, 12, 12, 12, 12, 36, 12, 36, 12, 12, 12, 36, 12, 12, 12, 12,
            12, 12, 12, 12, 12, 12 };

        int state = Utf8Accept;
        int codep;
        bool checking;

        public void Check(IByteBuffer buffer)
        {
            this.checking = true;
            buffer.ForEachByte(this);
        }

        public void Finish()
        {
            this.checking = false;
            this.codep = 0;
            if (this.state != Utf8Accept)
            {
                this.state = Utf8Accept;
                ThrowCorruptedFrameException();
            }
        }

        public bool Process(byte b)
        {
            byte type = Types[b & 0xFF];

            this.codep = this.state != Utf8Accept ? b & 0x3f | this.codep << 6 : 0xff >> type & b;

            this.state = States[this.state + type];

            if (this.state == Utf8Reject)
            {
                this.checking = false;
                ThrowCorruptedFrameException();
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowCorruptedFrameException() => throw new CorruptedFrameException("bytes are not UTF-8");

        public bool IsChecking => this.checking;
    }
}
