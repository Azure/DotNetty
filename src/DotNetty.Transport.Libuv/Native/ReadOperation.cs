// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable ConvertToAutoPropertyWhenPossible
namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using DotNetty.Buffers;

    sealed class ReadOperation : IDisposable
    {
        int status;
        bool endOfStream;
        IByteBuffer buffer;
        OperationException error;
        GCHandle pin;

        internal ReadOperation()
        {
            this.Reset();
        }

        internal IByteBuffer Buffer => this.buffer;

        internal OperationException Error => this.error;

        internal int Status => this.status;

        internal bool EndOfStream => this.endOfStream;

        internal void Complete(int statusCode, OperationException operationException)
        {
            this.Release();

            this.status = statusCode;
            this.endOfStream = statusCode == NativeMethods.EOF;
            this.error = operationException;
        }

        internal uv_buf_t GetBuffer(IByteBuffer buf)
        {
            Debug.Assert(!this.pin.IsAllocated);

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
            this.buffer = buf;

            return new uv_buf_t(arrayHandle + index, length);
        }

        internal void Reset()
        {
            this.status = 0;
            this.endOfStream = false;
            this.buffer = Unpooled.Empty;
            this.error = null;
        }

        void Release()
        {
            if (this.pin.IsAllocated)
            {
                this.pin.Free();
            }
        }

        public void Dispose()
        {
            this.Release();
            this.Reset();
        }
    }
}
