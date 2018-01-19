// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using DotNetty.Buffers;
    using DotNetty.Common;

    sealed class WriteRequest : NativeRequest
    {
        // The maximum number of uv_buf_t in one write request, this limits 
        // the libuv thread I/O time by channel write spin count. This is 
        // also to avoid the bufs array pin per write request. The buf array 
        // gets pinned upfront from the constructor and last for the lifetime 
        // of the request. 
        // Make this configurable if it is really a problem
        const int MaximumBufferCount = 32;

        internal static readonly uv_watcher_cb WriteCallback = OnWriteCallback;

        readonly ThreadLocalPool.Handle recyclerHandle;
        readonly List<GCHandle> handles;

        OperationException error;
        INativeUnsafe nativeUnsafe;
        GCHandle pin;
        uv_buf_t[] bufs;
        int bufferCount;

        public WriteRequest(ThreadLocalPool.Handle recyclerHandle, int maximumBufferCount = MaximumBufferCount)
            : base(uv_req_type.UV_WRITE, 0)
        {
            this.recyclerHandle = recyclerHandle;
            this.handles = new List<GCHandle>(maximumBufferCount);

            this.bufferCount = 0;
            this.bufs = new uv_buf_t[maximumBufferCount];
            this.pin = GCHandle.Alloc(this.bufs, GCHandleType.Pinned);
        }

        internal int Prepare(INativeUnsafe channelUnsafe, IByteBuffer buffer)
        {
            Debug.Assert(this.nativeUnsafe == null);

            int totalBytes = 0;

            // Do not pin the buffer again if it is already pinned
            IntPtr arrayHandle = buffer.AddressOfPinnedMemory();
            if (arrayHandle != IntPtr.Zero)
            {
                this.bufferCount = 1;
                totalBytes = buffer.ReadableBytes;
                this.bufs[0] = new uv_buf_t(arrayHandle + buffer.ReaderIndex, totalBytes);
            }
            else
            {
                if (buffer.IoBufferCount == 1)
                {
                    this.bufferCount = 1;
                    totalBytes = this.Prepare(0, buffer.GetIoBuffer());
                }
                else
                {
                    ArraySegment<byte>[] ioBuffers = buffer.GetIoBuffers();
                    this.bufferCount = Math.Min(ioBuffers.Length, this.bufs.Length);
                    for (int i = 0; i < this.bufferCount; i++)
                    {
                        totalBytes += this.Prepare(i, ioBuffers[i]);
                    }
                }
            }
            this.nativeUnsafe = channelUnsafe;
            return totalBytes;
        }

        internal int Prepare(INativeUnsafe channelUnsafe, ArraySegment<byte> ioBuffer)
        {
            Debug.Assert(this.nativeUnsafe == null);

            this.bufferCount = 1;
            int totalBytes = this.Prepare(0, ioBuffer);
            this.nativeUnsafe = channelUnsafe;
            return totalBytes;
        }

        internal int Prepare(INativeUnsafe channelUnsafe, List<ArraySegment<byte>> nioBuffers)
        {
            Debug.Assert(this.nativeUnsafe == null);

            int totalBytes = 0;
            if (nioBuffers.Count == 1)
            {
                totalBytes = this.Prepare(0, nioBuffers[0]);
                this.bufferCount = 1;
            }
            else
            {
                this.bufferCount = Math.Min(nioBuffers.Count, this.bufs.Length);
                for (int i = 0; i < this.bufferCount; i++)
                {
                    totalBytes += this.Prepare(i, nioBuffers[i]);
                }
            }
            this.nativeUnsafe = channelUnsafe;
            return totalBytes;
        }

        int Prepare(int index, ArraySegment<byte> ioBuffer)
        {
            GCHandle handle = GCHandle.Alloc(ioBuffer.Array, GCHandleType.Pinned);
            IntPtr arrayHandle = handle.AddrOfPinnedObject();

            int size = ioBuffer.Count;
            this.bufs[index] = new uv_buf_t(arrayHandle + ioBuffer.Offset, size);
            this.handles.Add(handle);
            return size;
        }

        internal ref uv_buf_t[] Bufs => ref this.bufs;

        internal ref int BufferCount => ref this.bufferCount;

        internal OperationException Error => this.error;

        internal void Release()
        {
            this.ReleaseHandles();

            this.nativeUnsafe = null;
            this.error = null;
            this.recyclerHandle.Release(this);
        }

        void ReleaseHandles()
        {
            if (this.handles.Count > 0)
            {
                // ReSharper disable once ForCanBeConvertedToForeach
                for (int i = 0; i < this.handles.Count; i++)
                {
                    if (this.handles[i].IsAllocated)
                    {
                        this.handles[i].Free();
                    }
                }
                this.handles.Clear();
            }
        }

        protected override void Dispose(bool disposing)
        {
            this.ReleaseHandles();
            if (this.pin.IsAllocated)
            {
                this.pin.Free();
            }
            base.Dispose(disposing);
        }

        void OnWriteCallback(int status)
        {
            this.ReleaseHandles();
            if (status < 0)
            {
                this.error = NativeMethods.CreateError((uv_err_code)status);
            }
            this.nativeUnsafe.FinishWrite(this);
        }

        static void OnWriteCallback(IntPtr handle, int status)
        {
            var request = GetTarget<WriteRequest>(handle);
            request.OnWriteCallback(status);
        }
    }
}