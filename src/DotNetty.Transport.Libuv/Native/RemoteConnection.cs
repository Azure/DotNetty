// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using System;

    sealed class RemoteConnection
    {
        public RemoteConnection(NativeHandle client, Exception error)
        {
            this.Client = client;
            this.Error = error;
        }

        internal NativeHandle Client { get; }

        internal Exception Error { get; }
    }
}
