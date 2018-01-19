// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace DotNetty.Transport.Libuv.Native
{
    using System;

    abstract class ConnectRequest : NativeRequest
    {
        protected static readonly uv_watcher_cb WatcherCallback = OnWatcherCallback;

        OperationException error;

        protected ConnectRequest() : base(uv_req_type.UV_CONNECT, 0)
        {
        }

        protected abstract void OnWatcherCallback();

        internal OperationException Error => this.error;

        static void OnWatcherCallback(IntPtr handle, int status)
        {
            var request = GetTarget<ConnectRequest>(handle);
            if (status < 0)
            {
                request.error = NativeMethods.CreateError((uv_err_code)status);
            }
            request.OnWatcherCallback();
        }
    }
}
