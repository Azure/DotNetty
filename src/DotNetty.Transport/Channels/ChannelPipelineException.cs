// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;

    class ChannelPipelineException : Exception
    {
        public ChannelPipelineException(string message)
            : base(message)
        {
        }

        public ChannelPipelineException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}