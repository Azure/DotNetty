// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Diagnostics;

    abstract class ConnectRequest : NativeRequest
    {
        protected static readonly uv_watcher_cb WatcherCallback = OnWatcherCallback;

        protected ConnectRequest() : base(uv_req_type.UV_CONNECT, 0)
        {
        }

        protected abstract void OnWatcherCallback();

        internal OperationException Error { get; private set; }

        static void OnWatcherCallback(IntPtr handle, int status)
        {
            Debug.Assert(handle != IntPtr.Zero);

            var request = GetTarget<ConnectRequest>(handle);
            request.Error = status < 0 ? NativeMethods.CreateError((uv_err_code)status) : null;

            request.OnWatcherCallback();
        }
    }
}
