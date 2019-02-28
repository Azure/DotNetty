// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Http
{
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    public class DefaultHttpContent : DefaultHttpObject, IHttpContent
    {
        readonly IByteBuffer content;

        public DefaultHttpContent(IByteBuffer content)
        {
            Contract.Requires(content != null);

            this.content = content;
        }

        public IByteBuffer Content => this.content;

        public IByteBufferHolder Copy() => this.Replace(this.content.Copy());

        public IByteBufferHolder Duplicate() => this.Replace(this.content.Duplicate());

        public IByteBufferHolder RetainedDuplicate() => this.Replace(this.content.RetainedDuplicate());

        public virtual IByteBufferHolder Replace(IByteBuffer buffer) => new DefaultHttpContent(buffer);

        public int ReferenceCount => this.content.ReferenceCount;

        public IReferenceCounted Retain()
        {
            this.content.Retain();
            return this;
        }

        public IReferenceCounted Retain(int increment)
        {
            this.content.Retain(increment);
            return this;
        }

        public IReferenceCounted Touch()
        {
            this.content.Touch();
            return this;
        }

        public IReferenceCounted Touch(object hint)
        {
            this.content.Touch(hint);
            return this;
        }

        public bool Release() => this.content.Release();

        public bool Release(int decrement) => this.content.Release(decrement);

        public override string ToString() => $"{StringUtil.SimpleClassName(this)} (data: {this.content}, decoderResult: {this.Result})";
    }
}
