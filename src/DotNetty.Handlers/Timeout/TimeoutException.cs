// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers
{
    using System.IO;

    public class TimeoutException : IOException
    {
        public TimeoutException(string message)
            :base(message)
        {
        }

        public TimeoutException()
        {
        }
    }
}

