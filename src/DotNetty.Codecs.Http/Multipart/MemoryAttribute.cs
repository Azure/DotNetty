// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Multipart
{
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    public class MemoryAttribute : AbstractMemoryHttpData, IAttribute
    {
        public MemoryAttribute(string name) 
            : this(name, HttpConstants.DefaultEncoding)
        {
        }

        public MemoryAttribute(string name, long definedSize) 
            : this(name, definedSize, HttpConstants.DefaultEncoding)
        {
        }

        public MemoryAttribute(string name, Encoding charset)
            : base(name, charset, 0)
        {
        }

        public MemoryAttribute(string name, long definedSize, Encoding charset)
            : base(name, charset, definedSize)
        {
        }

        public MemoryAttribute(string name, string value) 
            : this(name, value, HttpConstants.DefaultEncoding)
        {
        }

        public MemoryAttribute(string name, string value, Encoding contentEncoding)
            : base(name, contentEncoding, 0)
        {
            this.Value = value;
        }

        public override HttpDataType DataType => HttpDataType.Attribute;

        public string Value
        {
            get => this.GetByteBuffer().ToString(this.Charset);
            set
            {
                Contract.Requires(value != null);

                byte[] bytes = this.Charset.GetBytes(value);
                this.CheckSize(bytes.Length);
                IByteBuffer buffer = Unpooled.WrappedBuffer(bytes);
                if (this.DefinedSize > 0)
                {
                    this.DefinedSize = buffer.ReadableBytes;
                }
                this.SetContent(buffer);
            }
        }

        public override void AddContent(IByteBuffer buffer, bool last)
        {
            int localsize = buffer.ReadableBytes;
            this.CheckSize(this.Size + localsize);
            if (this.DefinedSize > 0 && this.DefinedSize < this.Size + localsize)
            {
                this.DefinedSize = this.Size + localsize;
            }
            base.AddContent(buffer, last);
        }

        public override int GetHashCode() => this.Name.GetHashCode();

        public override bool Equals(object obj)
        {
            if (obj is IAttribute attribute)
            {
                return this.Name.Equals(attribute.Name, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        public override int CompareTo(IInterfaceHttpData other)
        {
            if (!(other is IAttribute))
            {
                throw new ArgumentException($"Cannot compare {this.DataType}  with {other.DataType}");
            }

            return this.CompareTo((IAttribute)other);
        }

        public int CompareTo(IAttribute attribute) => string.Compare(this.Name, attribute.Name, StringComparison.OrdinalIgnoreCase);

        public override string ToString() => $"{this.Name} = {this.Value}";

        public override IByteBufferHolder Copy()
        {
            IByteBuffer content = this.Content;
            return this.Replace(content?.Copy());
        }

        public override IByteBufferHolder Duplicate()
        {
            IByteBuffer content = this.Content;
            return this.Replace(content?.Duplicate());
        }

        public override IByteBufferHolder RetainedDuplicate()
        {
            IByteBuffer content = this.Content;
            if (content != null)
            {
                content = content.RetainedDuplicate();
                bool success = false;
                try
                {
                    var duplicate = (IAttribute)this.Replace(content);
                    success = true;
                    return duplicate;
                }
                finally
                {
                    if (!success)
                    {
                        content.Release();
                    }
                }
            }
            else
            {
                return this.Replace(null);
            }
        }

        public override IByteBufferHolder Replace(IByteBuffer content)
        {
            var attr = new MemoryAttribute(this.Name);
            attr.Charset = this.Charset;
            if (content != null)
            {
                try
                {
                    attr.SetContent(content);
                }
                catch (IOException e)
                {
                    throw new ChannelException(e);
                }
            }
            return attr;
        }
    }
}