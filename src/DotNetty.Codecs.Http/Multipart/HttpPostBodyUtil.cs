// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
namespace DotNetty.Codecs.Http.Multipart
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    static class HttpPostBodyUtil
    {
        public static readonly int ChunkSize = 8096;

        public static readonly string DefaultBinaryContentType = "application/octet-stream";

        public static readonly string DefaultTextContentType = "text/plain";

        public sealed class TransferEncodingMechanism
        {
            // Default encoding
            public static readonly TransferEncodingMechanism Bit7 = new TransferEncodingMechanism("7bit");

            // Short lines but not in ASCII - no encoding
            public static readonly TransferEncodingMechanism Bit8 = new TransferEncodingMechanism("8bit");

            // Could be long text not in ASCII - no encoding
            public static readonly TransferEncodingMechanism Binary = new TransferEncodingMechanism("binary");

            readonly string value;

            TransferEncodingMechanism(string value)
            {
                this.value = value;
            }

            public string Value => this.value;

            public override string ToString() => this.value;
        }

        internal class SeekAheadOptimize
        {
            internal byte[] Bytes;
            internal int ReaderIndex;
            internal int Pos;
            internal int OrigPos;
            internal int Limit;
            internal IByteBuffer Buffer;

            internal SeekAheadOptimize(IByteBuffer buffer)
            {
                if (!buffer.HasArray)
                {
                    throw new ArgumentException("buffer hasn't backing byte array");
                }
                this.Buffer = buffer;
                this.Bytes = buffer.Array;
                this.ReaderIndex = buffer.ReaderIndex;
                this.OrigPos = this.Pos = buffer.ArrayOffset + this.ReaderIndex;
                this.Limit = buffer.ArrayOffset + buffer.WriterIndex;
            }

            internal void SetReadPosition(int minus)
            {
                this.Pos -= minus;
                this.ReaderIndex = this.GetReadPosition(this.Pos);
                this.Buffer.SetReaderIndex(this.ReaderIndex);
            }

            internal int GetReadPosition(int index) => index - this.OrigPos + this.ReaderIndex;
        }

        internal static int FindNonWhitespace(ICharSequence sb, int offset)
        {
            int result;
            for (result = offset; result < sb.Count; result++)
            {
                if (!char.IsWhiteSpace(sb[result]))
                {
                    break;
                }
            }

            return result;
        }

        internal static int FindWhitespace(ICharSequence sb, int offset)
        {
            int result;
            for (result = offset; result < sb.Count; result++)
            {
                if (char.IsWhiteSpace(sb[result]))
                {
                    break;
                }
            }

            return result;
        }

        internal static int FindEndOfString(ICharSequence sb)
        {
            int result;
            for (result = sb.Count; result > 0; result--)
            {
                if (!char.IsWhiteSpace(sb[result - 1]))
                {
                    break;
                }
            }

            return result;
        }
    }
}
