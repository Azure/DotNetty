// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Compression
{
    using System;

    public class DecompressionException : DecoderException
    {
        public DecompressionException(string message) 
            : base(message)
        {
        }

        public DecompressionException(string message, Exception exception) 
            : base(message, exception)
        {
        }
    }
}
