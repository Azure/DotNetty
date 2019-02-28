// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Messages
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.Contracts;
    using System.Text;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    public sealed class ArrayRedisMessage : AbstractReferenceCounted, IArrayRedisMessage
    {
        public static readonly IArrayRedisMessage Null = new NullArrayRedisMessage();
        public static readonly IArrayRedisMessage Empty = new EmptyArrayRedisMessage();

        public ArrayRedisMessage(IList<IRedisMessage> childMessages)
        {
            Contract.Requires(childMessages != null);
            // do not retain here. children are already retained when created.
            this.Children = childMessages;
        }

        public IList<IRedisMessage> Children { get; }

        public bool IsNull => false;

        protected override void Deallocate()
        {
            foreach (IRedisMessage message in this.Children)
            {
                ReferenceCountUtil.Release(message);
            }
        }

        public override IReferenceCounted Touch(object hint)
        {
            foreach (IRedisMessage message in this.Children)
            {
                ReferenceCountUtil.Touch(message);
            }
            return this;
        }

        public override string ToString() =>
            new StringBuilder(StringUtil.SimpleClassName(this))
                .Append('[')
                .Append("children=")
                .Append(this.Children.Count)
                .Append(']')
                .ToString();

        sealed class NullArrayRedisMessage : IArrayRedisMessage
        {
            public int ReferenceCount => 1;

            public bool IsNull => true;

            public IReferenceCounted Retain() => this;

            public IReferenceCounted Retain(int increment) => this;

            public IReferenceCounted Touch() => this;

            public IReferenceCounted Touch(object hint) => this;

            public bool Release() => false;

            public bool Release(int decrement) => false;

            public IList<IRedisMessage> Children => ImmutableList<IRedisMessage>.Empty;

            public override string ToString() => nameof(NullArrayRedisMessage);
        }

        sealed class EmptyArrayRedisMessage : IArrayRedisMessage
        {
            public int ReferenceCount => 1;

            public bool IsNull => false;

            public IReferenceCounted Retain() => this;

            public IReferenceCounted Retain(int increment) => this;

            public IReferenceCounted Touch() => this;

            public IReferenceCounted Touch(object hint) => this;

            public bool Release() => false;

            public bool Release(int decrement) => false;

            public IList<IRedisMessage> Children => ImmutableList<IRedisMessage>.Empty;

            public override string ToString() => nameof(EmptyArrayRedisMessage);
        }
    }
}