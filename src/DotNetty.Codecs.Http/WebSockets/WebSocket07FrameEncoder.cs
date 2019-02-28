// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    public class WebSocket07FrameEncoder : WebSocket08FrameEncoder
    {
        public WebSocket07FrameEncoder(bool maskPayload)
            : base(maskPayload)
        {
        }
    }
}
