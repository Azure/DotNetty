// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Timeout
{
    public sealed class ReadTimeoutException : TimeoutException
    {
        public readonly static ReadTimeoutException Instance = new ReadTimeoutException();

        public ReadTimeoutException(string message)
            :base(message)
        {
        }

        public ReadTimeoutException()
        {
        }
    }
}

