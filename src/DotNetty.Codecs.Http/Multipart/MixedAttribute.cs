// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Multipart
{
    using System;
    using System.IO;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common;

    public class MixedAttribute : IAttribute
    {
        IAttribute attribute;

        readonly long limitSize;
        long maxSize = DefaultHttpDataFactory.MaxSize;

        public MixedAttribute(string name, long limitSize)
            : this(name, limitSize, HttpConstants.DefaultEncoding)
        {
        }

        public MixedAttribute(string name, long definedSize, long limitSize)
            : this(name, definedSize, limitSize, HttpConstants.DefaultEncoding)
        {
        }

        public MixedAttribute(string name, long limitSize, Encoding contentEncoding)
        {
            this.limitSize = limitSize;
            this.attribute = new MemoryAttribute(name, contentEncoding);
        }

        public MixedAttribute(string name, long definedSize, long limitSize, Encoding contentEncoding)
        {
            this.limitSize = limitSize;
            this.attribute = new MemoryAttribute(name, definedSize, contentEncoding);
        }

        public MixedAttribute(string name, string value, long limitSize)
            : this(name, value, limitSize, HttpConstants.DefaultEncoding)
        {
        }

        public MixedAttribute(string name, string value, long limitSize, Encoding charset)
        {
            this.limitSize = limitSize;
            if (value.Length > this.limitSize)
            {
                try
                {
                    this.attribute = new DiskAttribute(name, value, charset);
                }
                catch (IOException e)
                {
                    // revert to Memory mode
                    try
                    {
                        this.attribute = new MemoryAttribute(name, value, charset);
                    }
                    catch (IOException)
                    {
                        throw new ArgumentException($"{name}", e);
                    }
                }
            }
            else
            {
                try
                {
                    this.attribute = new MemoryAttribute(name, value, charset);
                }
                catch (IOException e)
                {
                    throw new ArgumentException($"{name}", e);
                }
            }
        }


        public long MaxSize
        {
            get => this.maxSize;
            set
            {
                this.maxSize = value;
                this.attribute.MaxSize = this.maxSize;
            }
        }

        public void CheckSize(long newSize)
        {
            if (this.maxSize >= 0 && newSize > this.maxSize)
            {
                throw new IOException($"Size exceed allowed maximum capacity of {this.maxSize}");
            }
        }

        public void AddContent(IByteBuffer buffer, bool last)
        {
            if (this.attribute is MemoryAttribute memoryAttribute)
            {
                this.CheckSize(this.attribute.Length + buffer.ReadableBytes);
                if (this.attribute.Length + buffer.ReadableBytes > this.limitSize)
                {
                    var diskAttribute = new DiskAttribute(this.attribute.Name, this.attribute.DefinedLength);
                    diskAttribute.MaxSize = this.maxSize;
                    if (memoryAttribute.GetByteBuffer() != null)
                    {
                        diskAttribute.AddContent(memoryAttribute.GetByteBuffer(), false);
                    }
                    this.attribute = diskAttribute;
                }
            }
            this.attribute.AddContent(buffer, last);
        }

        public void Delete() => this.attribute.Delete();

        public byte[] GetBytes() => this.attribute.GetBytes();

        public IByteBuffer GetByteBuffer() => this.attribute.GetByteBuffer();

        public Encoding Charset
        {
            get => this.attribute.Charset;
            set => this.attribute.Charset = value;
        }

        public string GetString() => this.attribute.GetString();

        public string GetString(Encoding charset) => this.attribute.GetString(charset);

        public bool IsCompleted => this.attribute.IsCompleted;

        public bool IsInMemory => this.attribute.IsInMemory;

        public long Length => this.attribute.Length;

        public long DefinedLength => this.attribute.DefinedLength;

        public bool RenameTo(FileStream destination) => this.attribute.RenameTo(destination);

        public void SetContent(IByteBuffer buffer)
        {
            this.CheckSize(buffer.ReadableBytes);
            if (buffer.ReadableBytes > this.limitSize)
            {
                if (this.attribute is MemoryAttribute)
                {
                    // change to Disk
                    this.attribute = new DiskAttribute(this.attribute.Name, this.attribute.DefinedLength);
                    this.attribute.MaxSize = this.maxSize;
                }
            }
            this.attribute.SetContent(buffer);
        }

        public void SetContent(Stream source)
        {
            this.CheckSize(source.Length);
            if (source.Length > this.limitSize)
            {
                if (this.attribute is MemoryAttribute)
                {
                    // change to Disk
                    this.attribute = new DiskAttribute(this.attribute.Name, this.attribute.DefinedLength);
                    this.attribute.MaxSize = this.maxSize;
                }
            }
            this.attribute.SetContent(source);
        }

        public HttpDataType DataType => this.attribute.DataType;

        public string Name => this.attribute.Name;

        // ReSharper disable once NonReadonlyMemberInGetHashCode
        public override int GetHashCode() => this.attribute.GetHashCode();

        public override bool Equals(object obj) => this.attribute.Equals(obj);

        public int CompareTo(IInterfaceHttpData other) => this.attribute.CompareTo(other);

        public override string ToString() => $"Mixed: {this.attribute}";

        public string Value
        {
            get => this.attribute.Value;
            set
            {
                if (value != null)
                {
                    byte[] bytes = this.Charset != null
                        ? this.Charset.GetBytes(value)
                        : HttpConstants.DefaultEncoding.GetBytes(value);
                    this.CheckSize(bytes.Length);
                }

                this.attribute.Value = value;
            }
        }

        public IByteBuffer GetChunk(int length) => this.attribute.GetChunk(length);

        public FileStream GetFile() => this.attribute.GetFile();

        public IByteBufferHolder Copy() => this.attribute.Copy();

        public IByteBufferHolder Duplicate() => this.attribute.Duplicate();

        public IByteBufferHolder RetainedDuplicate() => this.attribute.RetainedDuplicate();

        public IByteBufferHolder Replace(IByteBuffer content) => this.attribute.Replace(content);

        public IByteBuffer Content => this.attribute.Content;

        public int ReferenceCount => this.attribute.ReferenceCount;

        public IReferenceCounted Retain()
        {
            this.attribute.Retain();
            return this;
        }

        public IReferenceCounted Retain(int increment)
        {
            this.attribute.Retain(increment);
            return this;
        }

        public IReferenceCounted Touch()
        {
            this.attribute.Touch();
            return this;
        }

        public IReferenceCounted Touch(object hint)
        {
            this.attribute.Touch(hint);
            return this;
        }

        public bool Release() => this.attribute.Release();

        public bool Release(int decrement) => this.attribute.Release(decrement);
    }
}
