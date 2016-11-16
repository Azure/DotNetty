// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;
    using DotNetty.Common.Utilities;

    public sealed class ReadOnlyByteBufferStream : Stream
    {
        readonly IByteBuffer buffer;
        bool releaseReferenceOnClosure;

        public ReadOnlyByteBufferStream(IByteBuffer buffer, bool releaseReferenceOnClosure)
        {
            Contract.Requires(buffer != null);

            this.buffer = buffer;
            this.releaseReferenceOnClosure = releaseReferenceOnClosure;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override int Read(byte[] output, int offset, int count)
        {
            int read = Math.Min(count - offset, this.buffer.ReadableBytes);
            this.buffer.ReadBytes(output, offset, read);
            return read;
        }

        public override void Flush()
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (this.releaseReferenceOnClosure)
            {
                this.releaseReferenceOnClosure = false;
                if (disposing)
                {
                    this.buffer.Release();
                }
                else
                {
                    ReferenceCountUtil.SafeRelease(this.buffer);
                }
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] input, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
