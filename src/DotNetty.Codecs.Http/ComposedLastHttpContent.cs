// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Http
{
    using DotNetty.Buffers;
    using DotNetty.Common;

    public sealed class ComposedLastHttpContent : ILastHttpContent
    {
        readonly HttpHeaders trailingHeaders;
        DecoderResult result;

        internal ComposedLastHttpContent(HttpHeaders trailingHeaders)
        {
            this.trailingHeaders = trailingHeaders;
        }

        public HttpHeaders TrailingHeaders => this.trailingHeaders;

        public IByteBufferHolder Copy()
        {
            var content = new DefaultLastHttpContent(Unpooled.Empty);
            content.TrailingHeaders.Set(this.trailingHeaders);
            return content;
        }

        public IByteBufferHolder Duplicate() => this.Copy();

        public IByteBufferHolder RetainedDuplicate() => this.Copy();

        public IByteBufferHolder Replace(IByteBuffer content)
        {
            var dup = new DefaultLastHttpContent(content);
            dup.TrailingHeaders.SetAll(this.trailingHeaders);
            return dup;
        }

        public IReferenceCounted Retain() => this;

        public IReferenceCounted Retain(int increment) => this;

        public IReferenceCounted Touch() => this;

        public IReferenceCounted Touch(object hint) => this;

        public IByteBuffer Content => Unpooled.Empty;

        public DecoderResult Result
        {
            get => this.result;
            set => this.result = value;
        }

        public int ReferenceCount => 1;

        public bool Release() => false;

        public bool Release(int decrement) => false;
    }
}
