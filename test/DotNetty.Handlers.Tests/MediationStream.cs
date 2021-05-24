// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tests
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    class MediationStream : Stream
    {
        readonly Func<ArraySegment<byte>, Task<int>> readDataFunc;
        readonly Func<ArraySegment<byte>, Task> writeDataFunc;
        readonly Action disposeFunc;

        public MediationStream(Func<ArraySegment<byte>, Task<int>> readDataFunc, Func<ArraySegment<byte>, Task> writeDataFunc, Action disposeFunc = null)
        {
            this.readDataFunc = readDataFunc;
            this.writeDataFunc = writeDataFunc;
            this.disposeFunc = disposeFunc;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => this.readDataFunc(new ArraySegment<byte>(buffer, offset, count));

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => this.writeDataFunc(new ArraySegment<byte>(buffer, offset, count));

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                disposeFunc?.Invoke();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }
    }
}