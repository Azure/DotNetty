// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{
    using System;

    /// <summary>
    ///     Special exception which will get thrown if a packet is
    ///     received that not looks like a TLS/SSL record. A user can check for
    ///     this <see cref="NotSslRecordException" /> and so detect if one peer tries to
    ///     use secure and the other plain connection.
    /// </summary>
    public class NotSslRecordException : Exception
    {
        public NotSslRecordException()
            : base(string.Empty)
        {
        }

        public NotSslRecordException(string message)
            : base(message)
        {
        }

        public NotSslRecordException(Exception cause)
            : base(string.Empty, cause)
        {
        }

        public NotSslRecordException(string message, Exception cause)
            : base(message, cause)
        {
        }
    }
}