// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;

    public class DecoderException : CodecException
    {
        public DecoderException(string message)
            : base(message)
        {
        }

        public DecoderException(Exception cause)
            : base(null, cause)
        {
        }

        public DecoderException(string message, Exception cause)
            : base(message, cause)
        {
        }
    }
}