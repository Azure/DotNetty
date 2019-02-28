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

    public class DiskAttribute : AbstractDiskHttpData, IAttribute
    {
        public static string DiskBaseDirectory;
        public static bool DeleteOnExitTemporaryFile = true;
        public static readonly string FilePrefix = "Attr_";
        public static readonly string FilePostfix = ".att";

        public DiskAttribute(string name)
            : this(name, HttpConstants.DefaultEncoding)
        {
        }

        public DiskAttribute(string name, long definedSize)
            : this(name, definedSize, HttpConstants.DefaultEncoding)
        {
        }

        public DiskAttribute(string name, Encoding charset)
            : base(name, charset, 0)
        {
        }

        public DiskAttribute(string name, long definedSize, Encoding charset)
            : base(name, charset, definedSize)
        {
        }

        public DiskAttribute(string name, string value)
            : this(name, value, HttpConstants.DefaultEncoding)
        {
        }

        public DiskAttribute(string name, string value, Encoding charset)
            : base(name, charset, 0) // Attribute have no default size
        {
            this.Value = value;
        }

        public override HttpDataType DataType => HttpDataType.Attribute;

        public string Value
        {
            get
            {
                byte[] bytes = this.GetBytes();
                return this.Charset.GetString(bytes);
            }
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
            long newDefinedSize = this.Size + buffer.ReadableBytes;
            this.CheckSize(newDefinedSize);
            if (this.DefinedSize > 0 && this.DefinedSize < newDefinedSize)
            {
                this.DefinedSize = newDefinedSize;
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
                throw new ArgumentException($"Cannot compare {this.DataType} with {other.DataType}");
            }

            return this.CompareTo((IAttribute)other);
        }

        public int CompareTo(IAttribute attribute) => string.Compare(this.Name, attribute.Name, StringComparison.OrdinalIgnoreCase);

        public override string ToString()
        {
            try
            {
                return $"{this.Name}={this.Value}";
            }
            catch (IOException e)
            {
                return $"{this.Name}={e}";
            }
        }

        protected override bool DeleteOnExit => DeleteOnExitTemporaryFile;

        protected override string BaseDirectory => DiskBaseDirectory;

        protected override string DiskFilename => $"{this.Name}{this.Postfix}";

        protected override string Postfix => FilePostfix;

        protected override string Prefix => FilePrefix;

        public override IByteBufferHolder Copy() => this.Replace(this.Content?.Copy());

        public override IByteBufferHolder Duplicate() => this.Replace(this.Content?.Duplicate());

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
            var attr = new DiskAttribute(this.Name);
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
