// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;

    /// <summary>
    ///     A <see cref="DecoderException" /> which is thrown when the received frame data could not
    ///     be decoded by an inbound handler.
    /// </summary>
    public class CorruptedFrameException : DecoderException
    {
        public CorruptedFrameException(string message)
            : base(message)
        {
        }

        public CorruptedFrameException(Exception cause)
            : base(cause)
        {
        }
    }
}