// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using DotNetty.Common;

    sealed class WriteRequest : NativeRequest
    {
        internal static readonly uv_watcher_cb WriteCallback = OnWriteCallback;

        readonly ThreadLocalPool.Handle recyclerHandle;
        readonly List<GCHandle> handles;

        INativeUnsafe nativeUnsafe;
        uv_buf_t[] bufs;

        public WriteRequest(ThreadLocalPool.Handle recyclerHandle) 
            : base(uv_req_type.UV_WRITE, 0)
        {
            this.recyclerHandle = recyclerHandle;
            this.handles = new List<GCHandle>();
            this.bufs = new uv_buf_t[1];
        }

        internal void Prepare(INativeUnsafe channelUnsafe, List<ArraySegment<byte>> nioBuffers)
        {
            if (nioBuffers.Count == 1)
            {
                ArraySegment<byte> buffer = nioBuffers[0];
                GCHandle handle = GCHandle.Alloc(buffer.Array, GCHandleType.Pinned);
                IntPtr arrayHandle = handle.AddrOfPinnedObject();

                this.bufs[0] = new uv_buf_t(arrayHandle + buffer.Offset, buffer.Count);
                this.handles.Add(handle);
            }
            else
            {
                if (this.bufs.Length < nioBuffers.Count)
                {
                    this.bufs = new uv_buf_t[nioBuffers.Count];
                }

                for (int i = 0; i < nioBuffers.Count; i++)
                {
                    GCHandle handle = GCHandle.Alloc(nioBuffers[i].Array, GCHandleType.Pinned);
                    IntPtr arrayHandle = handle.AddrOfPinnedObject();

                    this.bufs[i] = new uv_buf_t(arrayHandle + nioBuffers[i].Offset, nioBuffers[i].Count);
                    this.handles.Add(handle);
                }

                GCHandle bufArray = GCHandle.Alloc(this.bufs, GCHandleType.Pinned);
                this.handles.Add(bufArray);
            }

            this.nativeUnsafe = channelUnsafe;
        }

        internal ref uv_buf_t[] Bufs => ref this.bufs;

        internal OperationException Error { get; private set; }

        internal void Release()
        {
            this.ReleaseHandles();

            this.nativeUnsafe = null;
            this.Error = null;
            this.recyclerHandle.Release(this);
        }

        void ReleaseHandles()
        {
            if (this.handles.Count == 0)
            {
                return;
            }

            foreach (GCHandle handle in this.handles)
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }
            this.handles.Clear();
        }

        void OnWriteCallback(int status)
        {
            this.ReleaseHandles();

            if (status < 0)
            {
                this.Error = NativeMethods.CreateError((uv_err_code)status);
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
