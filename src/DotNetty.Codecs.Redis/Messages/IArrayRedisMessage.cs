// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Messages
{
    using System.Collections.Generic;
    using DotNetty.Common;

    public interface IArrayRedisMessage : IReferenceCounted, IRedisMessage
    {
        bool IsNull { get; }

        IList<IRedisMessage> Children { get; }
    }
}
