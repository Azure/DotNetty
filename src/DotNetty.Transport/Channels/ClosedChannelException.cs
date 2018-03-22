// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.IO;

    public class ClosedChannelException : IOException
    {
        public ClosedChannelException()
        {
        }

        public ClosedChannelException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}