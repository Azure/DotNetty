// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;

    [Flags]
    public enum PropagationDirections
    {
        None = 0,
        Inbound = 1,
        Outbound = 2
    }
}