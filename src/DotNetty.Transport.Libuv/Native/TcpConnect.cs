// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using System.Diagnostics.Contracts;
    using System.Net;

    sealed class TcpConnect : ConnectRequest
    {
        readonly INativeUnsafe nativeUnsafe;

        public TcpConnect(INativeUnsafe nativeUnsafe, IPEndPoint remoteEndPoint)
        {
            Contract.Requires(nativeUnsafe != null);
            Contract.Requires(remoteEndPoint != null);

            NativeMethods.GetSocketAddress(remoteEndPoint, out sockaddr addr);
            int result = NativeMethods.uv_tcp_connect(
                this.Handle,
                nativeUnsafe.UnsafeHandle,
                ref addr,
                WatcherCallback);

            if (result < 0)
            {
                throw NativeMethods.CreateError((uv_err_code)result);
            }

            this.nativeUnsafe = nativeUnsafe;
        }

        protected override void OnWatcherCallback()
        {
            this.nativeUnsafe.FinishConnect(this);
        }
    }
}
