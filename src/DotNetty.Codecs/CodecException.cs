// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;

    /// <summary>
    ///     An <see cref="Exception" /> which is thrown by a codec.
    /// </summary>
    public class CodecException : Exception
    {
        public CodecException()
        {
        }

        public CodecException(string message, Exception innereException)
            : base(message, innereException)
        {
        }

        public CodecException(string message)
            : base(message)
        {
        }

        public CodecException(Exception innerException)
            : base(null, innerException)
        {
        }
    }
}