// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;

    /// <summary>
    ///     A <see cref="DecoderException" /> which is thrown when the length of the frame
    ///     decoded is greater than the allowed maximum.
    /// </summary>
    public class TooLongFrameException : DecoderException
    {
        public TooLongFrameException(string message)
            : base(message)
        {
        }

        public TooLongFrameException(Exception cause)
            : base(cause)
        {
        }
    }
}