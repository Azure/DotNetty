// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using DotNetty.Buffers;

    public sealed class ReadOperation : IDisposable
    {
        readonly INativeUnsafe nativeUnsafe;
        readonly IByteBuffer buffer;

        OperationException error;
        int status;
        bool endOfStream;

        GCHandle pin;

        internal ReadOperation(INativeUnsafe nativeUnsafe, IByteBuffer buffer)
        {
            this.nativeUnsafe = nativeUnsafe;
            this.buffer = buffer;
            this.status = 0;
            this.endOfStream = false;
        }

        internal IByteBuffer Buffer => this.buffer;

        internal OperationException Error => this.error;

        internal int Status => this.status;

        internal bool EndOfStream => this.endOfStream;

        internal void Complete(int statusCode, OperationException exception)
        {
            this.Release();

            this.status = statusCode;
            this.endOfStream = statusCode == (int)uv_err_code.UV_EOF;
            this.error = exception;
            this.nativeUnsafe.FinishRead(this);
        }

        internal uv_buf_t GetBuffer()
        {
            Debug.Assert(!this.pin.IsAllocated);

            IByteBuffer buf = this.Buffer;

            // Do not pin the buffer again if it is already pinned
            IntPtr arrayHandle = buf.AddressOfPinnedMemory();
            int index = buf.WriterIndex;

            if (arrayHandle == IntPtr.Zero)
            {
                this.pin = GCHandle.Alloc(buf.Array, GCHandleType.Pinned);
                arrayHandle = this.pin.AddrOfPinnedObject();
                index += buf.ArrayOffset;
            }
            int length = buf.WritableBytes;

            return new uv_buf_t(arrayHandle + index, length);
        }

        void Release()
        {
            if (this.pin.IsAllocated)
            {
                this.pin.Free();
            }
        }

        public void Dispose() => this.Release();
    }
}
