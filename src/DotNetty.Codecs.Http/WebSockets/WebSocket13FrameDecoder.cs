﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    public class WebSocket13FrameDecoder : WebSocket08FrameDecoder
    {
        public WebSocket13FrameDecoder(bool expectMaskedFrames, bool allowExtensions, int maxFramePayloadLength)
            : this(expectMaskedFrames, allowExtensions, maxFramePayloadLength, false)
        {
        }

        public WebSocket13FrameDecoder(bool expectMaskedFrames, bool allowExtensions,int maxFramePayloadLength,
            bool allowMaskMismatch)
            : base(expectMaskedFrames, allowExtensions, maxFramePayloadLength, allowMaskMismatch)
        {
        }
    }
}
