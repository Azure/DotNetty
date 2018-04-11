// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Multipart
{
    using System;

    public class ErrorDataDecoderException : DecoderException
    {
        public ErrorDataDecoderException(string message)
            : base(message)
        {
        }

        public ErrorDataDecoderException(Exception innerException)
            : base(innerException)
        {
        }

        public ErrorDataDecoderException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
