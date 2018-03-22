// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis
{
    using System;

    public class MessageAggregationException : InvalidOperationException
    {
        public MessageAggregationException()
        {
        }

        public MessageAggregationException(string message)
            : this(message, null)
        {
        }

        public MessageAggregationException(Exception exception)
            : this(null, exception)
        {
        }

        public MessageAggregationException(string message, Exception exception)
            : base(message, exception)
        {
        }
    }
}