// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Http.Multipart
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    sealed class InternalAttribute : AbstractReferenceCounted, IInterfaceHttpData
    {
        readonly List<IByteBuffer> value = new List<IByteBuffer>();
        readonly Encoding charset;
        int size;

        internal InternalAttribute(Encoding charset)
        {
            this.charset = charset;
        }

        public HttpDataType DataType => HttpDataType.InternalAttribute;

        public void AddValue(string stringValue)
        {
            Contract.Requires(stringValue != null);

            IByteBuffer buf = Unpooled.CopiedBuffer(this.charset.GetBytes(stringValue));
            this.value.Add(buf);
            this.size += buf.ReadableBytes;
        }

        public void AddValue(string stringValue, int rank)
        {
            Contract.Requires(stringValue != null);

            IByteBuffer buf = Unpooled.CopiedBuffer(this.charset.GetBytes(stringValue));
            this.value[rank] = buf;
            this.size += buf.ReadableBytes;
        }

        public void SetValue(string stringValue, int rank)
        {
            Contract.Requires(stringValue != null);

            IByteBuffer buf = Unpooled.CopiedBuffer(this.charset.GetBytes(stringValue));
            IByteBuffer old = this.value[rank];
            this.value[rank] = buf;
            if (old != null)
            {
                this.size -= old.ReadableBytes;
                old.Release();
            }
            this.size += buf.ReadableBytes;
        }

        public override int GetHashCode() => this.Name.GetHashCode();

        public override bool Equals(object obj)
        {
            if (obj is InternalAttribute attribute)
            {
                return this.Name.Equals(attribute.Name, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public int CompareTo(IInterfaceHttpData other)
        {
            if (!(other is InternalAttribute))
            {
                throw new ArgumentException($"Cannot compare {this.DataType} with {other.DataType}");
            }

            return this.CompareTo((InternalAttribute)other);
        }

        public int CompareTo(InternalAttribute other) => string.Compare(this.Name, other.Name, StringComparison.OrdinalIgnoreCase);

        public override string ToString()
        {
            var result = new StringBuilder();
            foreach (IByteBuffer buf in this.value)
            {
                result.Append(buf.ToString(this.charset));
            }
            
            return result.ToString();
        }

        public int Size => this.size;

        public IByteBuffer ToByteBuffer()
        {
            CompositeByteBuffer compositeBuffer = Unpooled.CompositeBuffer(this.value.Count);
            compositeBuffer.AddComponents(this.value);
            compositeBuffer.SetWriterIndex(this.size);
            compositeBuffer.SetReaderIndex(0);

            return compositeBuffer;
        }

        public string Name => nameof(InternalAttribute);

        protected override void Deallocate()
        {
            // Do nothing
        }

        protected override IReferenceCounted RetainCore(int increment)
        {
            foreach (IByteBuffer buf in this.value)
            {
                buf.Retain(increment);
            }
            return this;
        }

        public override IReferenceCounted Touch(object hint)
        {
            foreach (IByteBuffer buf in this.value)
            {
                buf.Touch(hint);
            }
            return this;
        }
    }
}
