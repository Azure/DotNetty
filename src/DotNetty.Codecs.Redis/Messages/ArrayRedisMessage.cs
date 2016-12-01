// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Messages
{
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    public sealed class ArrayRedisMessage : AbstractReferenceCounted, IRedisMessage
    {
        public static readonly ArrayRedisMessage Null = new ArrayRedisMessage();

        public static readonly ArrayRedisMessage Empty = new ArrayRedisMessage();

        ArrayRedisMessage()
            : this(new List<IRedisMessage>())
        {
        }

        public ArrayRedisMessage(IList<IRedisMessage> childMessages)
        {
            Contract.Requires(childMessages != null);

            this.Children = childMessages;
        }

        public IList<IRedisMessage> Children { get; }

        public bool IsNull => this == Null;

        public override IReferenceCounted Touch(object hint)
        {
            foreach (IRedisMessage message in this.Children)
            {
                ReferenceCountUtil.Touch(message);
            }

            return this;
        }

        protected override void Deallocate()
        {
            foreach (IRedisMessage message in this.Children)
            {
                ReferenceCountUtil.Release(message);
            }
        }
    }
}