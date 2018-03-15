// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ForCanBeConvertedToForeach
namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Transport.Channels;

    sealed class WriteRequest : NativeRequest, ChannelOutboundBuffer.IMessageProcessor
    {
        static readonly int BufferSize;
        static readonly uv_watcher_cb WriteCallback = OnWriteCallback;

        const int MaximumBytes = int.MaxValue;
        const int MaximumLimit = 64;

        static WriteRequest()
        {
#if NETSTANDARD1_3
            BufferSize = Marshal.SizeOf<uv_buf_t>();
#else
            BufferSize = Marshal.SizeOf(typeof(uv_buf_t));
#endif
        }

        readonly int maxBytes;
        readonly ThreadLocalPool.Handle recyclerHandle;
        readonly List<GCHandle> handles;

        IntPtr bufs;
        GCHandle pin;
        int count;
        int size;

        NativeChannel.INativeUnsafe nativeUnsafe;

        public WriteRequest(ThreadLocalPool.Handle recyclerHandle)
            : base(uv_req_type.UV_WRITE, BufferSize * MaximumLimit)
        {
            this.recyclerHandle = recyclerHandle;

            int offset = NativeMethods.GetSize(uv_req_type.UV_WRITE);
            IntPtr addr = this.Handle;

            this.maxBytes = MaximumBytes;
            this.bufs = addr + offset;
            this.pin = GCHandle.Alloc(addr, GCHandleType.Pinned);
            this.handles = new List<GCHandle>();
        }

        internal void DoWrite(NativeChannel.INativeUnsafe channelUnsafe, ChannelOutboundBuffer input)
        {
            Debug.Assert(this.nativeUnsafe == null);

            this.nativeUnsafe = channelUnsafe;
            input.ForEachFlushedMessage(this);
            this.DoWrite();
        }

        bool Add(IByteBuffer buf)
        {
            if (this.count == MaximumLimit)
            {
                return false;
            }

            int len = buf.ReadableBytes;
            if (len == 0)
            {
                return true;
            }

            if (this.maxBytes - len < this.size && this.count > 0)
            {
                return false;
            }

            IntPtr addr = IntPtr.Zero;
            if (buf.HasMemoryAddress)
            {
                addr = buf.AddressOfPinnedMemory();
            }

            if (addr != IntPtr.Zero)
            {
                this.Add(addr, buf.ReaderIndex, len);
            }
            else
            {
                int bufferCount = buf.IoBufferCount;
                if (MaximumLimit - bufferCount < this.count)
                {
                    return false;
                }

                if (bufferCount == 1)
                {
                    ArraySegment<byte> arraySegment = buf.GetIoBuffer();

                    byte[] array = arraySegment.Array;
                    GCHandle handle = GCHandle.Alloc(array, GCHandleType.Pinned);
                    this.handles.Add(handle);

                    addr = handle.AddrOfPinnedObject();
                    this.Add(addr, arraySegment.Offset, arraySegment.Count);
                }
                else
                {
                    ArraySegment<byte>[] segments = buf.GetIoBuffers();
                    for (int i = 0; i < segments.Length; i++)
                    {
                        GCHandle handle = GCHandle.Alloc(segments[i].Array, GCHandleType.Pinned);
                        this.handles.Add(handle);

                        addr = handle.AddrOfPinnedObject();
                        this.Add(addr, segments[i].Offset, segments[i].Count);
                    }
                }
            }
            return true;
        }

        void Add(IntPtr addr, int offset, int len)
        {
            IntPtr baseOffset = this.MemoryAddress(this.count);
            this.size += len;
            ++this.count;
            uv_buf_t.InitMemory(baseOffset, addr + offset, len);
        }

        unsafe void DoWrite()
        {
            int result = NativeMethods.uv_write(
                this.Handle,
                this.nativeUnsafe.UnsafeHandle,
                (uv_buf_t*)this.bufs,
                this.count,
                WriteCallback);

            if (result < 0)
            { 
                this.Release();
                NativeMethods.ThrowOperationException((uv_err_code)result);
            }
        }

        public bool ProcessMessage(object msg) => msg is IByteBuffer buf && this.Add(buf);

        void Release()
        {
            if (this.handles.Count > 0)
            {
                for (int i = 0; i < this.handles.Count; i++)
                {
                    if (this.handles[i].IsAllocated)
                    {
                        this.handles[i].Free();
                    }
                }
                this.handles.Clear();
            }

            this.nativeUnsafe = null;
            this.count = 0;
            this.size = 0;
            this.recyclerHandle.Release(this);
        }

        void OnWriteCallback(int status)
        {
            NativeChannel.INativeUnsafe @unsafe = this.nativeUnsafe;
            int bytesWritten = this.size;
            this.Release();

            OperationException error = null;
            if (status < 0)
            {
                error = NativeMethods.CreateError((uv_err_code)status);
            }
            @unsafe.FinishWrite(bytesWritten, error);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IntPtr MemoryAddress(int offset) => this.bufs + BufferSize * offset;

        static void OnWriteCallback(IntPtr handle, int status)
        {
            var request = GetTarget<WriteRequest>(handle);
            request.OnWriteCallback(status);
        }

        void Free()
        {
            this.Release();
            if (this.pin.IsAllocated)
            {
                this.pin.Free();
            }
            this.bufs = IntPtr.Zero;
        }

        protected override void Dispose(bool disposing)
        {
            if (this.bufs != IntPtr.Zero)
            {
                this.Free();
            }
            base.Dispose(disposing);
        }
    }
}