// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;

    public class EncoderException : CodecException
    {
        public EncoderException(string message)
            : base(message)
        {
        }

        public EncoderException(Exception innerException)
            : base(null, innerException)
        {
        }

        public EncoderException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}