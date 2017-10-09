// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Runtime.InteropServices;
    using DotNetty.Buffers;

    public sealed class ReadOperation : IDisposable
    {
        readonly INativeUnsafe nativeUnsafe;
        GCHandle array;

        internal ReadOperation(INativeUnsafe nativeUnsafe, IByteBuffer buffer)
        {
            this.nativeUnsafe = nativeUnsafe;
            this.Buffer = buffer;
            this.Status = 0;
            this.EndOfStream = false;
        }

        internal IByteBuffer Buffer { get; }

        internal OperationException Error { get; private set; }

        internal int Status { get; private set; }

        internal bool EndOfStream { get; private set; }

        internal void Complete(int status, OperationException error)
        {
            this.Release();

            this.Status = status;
            this.EndOfStream = status == (int)uv_err_code.UV_EOF;
            this.Error = error;
            this.nativeUnsafe.FinishRead(this);
        }

        internal uv_buf_t GetBuffer()
        {
            if (this.array.IsAllocated)
            {
                throw new InvalidOperationException(
                    $"{nameof(ReadOperation)} has already been initialized and not released yet.");
            }

            IByteBuffer buf = this.Buffer;
            this.array = GCHandle.Alloc(buf.Array, GCHandleType.Pinned);
            IntPtr arrayHandle = this.array.AddrOfPinnedObject();

            int index = buf.ArrayOffset + buf.WriterIndex;
            int length = buf.WritableBytes;

            return new uv_buf_t(arrayHandle + index, length);
        }

        void Release()
        {
            if (this.array.IsAllocated)
            {
                this.array.Free();
            }
        }

        public void Dispose() => this.Release();
    }
}
