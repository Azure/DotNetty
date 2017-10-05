// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Multipart
{
    using System;

    public class EndOfDataDecoderException : DecoderException
    {
        public EndOfDataDecoderException(string message)
            : base(message)
        {
        }

        public EndOfDataDecoderException(Exception innerException)
            : base(innerException)
        {
        }
    }
}
