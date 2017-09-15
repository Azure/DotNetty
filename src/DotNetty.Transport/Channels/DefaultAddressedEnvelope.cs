// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System.Diagnostics.Contracts;
    using System.Net;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    public class DefaultAddressedEnvelope<T> : IAddressedEnvelope<T>
    {
        public DefaultAddressedEnvelope(T content, EndPoint recipient)
            : this(content, null, recipient)
        {
        }

        public DefaultAddressedEnvelope(T content, EndPoint sender, EndPoint recipient)
        {
            Contract.Requires(content != null);
            Contract.Requires(sender != null || recipient != null);

            this.Content = content;
            this.Sender = sender;
            this.Recipient = recipient;
        }

        public T Content { get; }

        public EndPoint Sender { get; }

        public EndPoint Recipient { get; }

        public int ReferenceCount
        {
            get
            {
                var counted = this.Content as IReferenceCounted;
                return counted?.ReferenceCount ?? 1;
            }
        }

        public virtual IReferenceCounted Retain()
        {
            ReferenceCountUtil.Retain(this.Content);
            return this;
        }

        public virtual IReferenceCounted Retain(int increment)
        {
            ReferenceCountUtil.Retain(this.Content, increment);
            return this;
        }

        public virtual IReferenceCounted Touch()
        {
            ReferenceCountUtil.Touch(this.Content);
            return this;
        }

        public virtual IReferenceCounted Touch(object hint)
        {
            ReferenceCountUtil.Touch(this.Content, hint);
            return this;
        }

        public bool Release() => ReferenceCountUtil.Release(this.Content);

        public bool Release(int decrement) => ReferenceCountUtil.Release(this.Content, decrement);

        public override string ToString() => $"DefaultAddressedEnvelope<{typeof(T)}>"
            + (this.Sender != null
                ? $"({this.Sender} => {this.Recipient}, {this.Content})"
                : $"(=> {this.Recipient}, {this.Content})");
    }
}