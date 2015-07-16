// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System.IO;

    public class ConnectTimeoutException : IOException
    {
        public ConnectTimeoutException(string message)
            : base(message)
        {
        }

        public ConnectTimeoutException()
        {
        }
    }
}