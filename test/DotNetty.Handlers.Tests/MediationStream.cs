// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    class MediationStream : Stream
    {
        readonly Func<ArraySegment<byte>, Task<int>> readDataFunc;
        readonly Action<ArraySegment<byte>> writeDataFunc;

        public MediationStream(Func<ArraySegment<byte>, Task<int>> readDataFunc, Action<ArraySegment<byte>> writeDataFunc)
        {
            this.readDataFunc = readDataFunc;
            this.writeDataFunc = writeDataFunc;
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

        public override int Read(byte[] buffer, int offset, int count) => this.readDataFunc(new ArraySegment<byte>(buffer, offset, count)).Result;

        public override void Write(byte[] buffer, int offset, int count) => this.writeDataFunc(new ArraySegment<byte>(buffer, offset, count));

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