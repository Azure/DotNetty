// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Multipart
{
    using System.IO;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common;

    public class MixedFileUpload : IFileUpload
    {
        IFileUpload fileUpload;

        readonly long limitSize;
        readonly long definedSize;

        long maxSize = DefaultHttpDataFactory.MaxSize;


        public MixedFileUpload(string name, string fileName, string contentType, 
            string contentTransferEncoding, Encoding charset, long size, 
            long limitSize)
        {
            this.limitSize = limitSize;
            if (size > this.limitSize)
            {
                this.fileUpload = new DiskFileUpload(name, fileName, contentType,
                    contentTransferEncoding, charset, size);
            }
            else
            {
                this.fileUpload = new MemoryFileUpload(name, fileName, contentType,
                    contentTransferEncoding, charset, size);
            }
            this.definedSize = size;
        }

        public long MaxSize
        {
            get => this.maxSize;
            set
            {
                this.maxSize = value;
                this.fileUpload.MaxSize = value;
            }
        }

        public void CheckSize(long newSize)
        {
            if (this.maxSize >= 0 && newSize > this.maxSize)
            {
                throw new IOException($"{this.DataType} Size exceed allowed maximum capacity");
            }
        }

        public void AddContent(IByteBuffer buffer, bool last)
        {
            if (this.fileUpload is MemoryFileUpload) {
                this.CheckSize(this.fileUpload.Length + buffer.ReadableBytes);
                if (this.fileUpload.Length + buffer.ReadableBytes > this.limitSize)
                {
                    var diskFileUpload = new DiskFileUpload(
                        this.fileUpload.Name, this.fileUpload.FileName, 
                        this.fileUpload.ContentType, 
                        this.fileUpload.ContentTransferEncoding, this.fileUpload.Charset,
                        this.definedSize);
                    diskFileUpload.MaxSize = this.maxSize;
                    IByteBuffer data = this.fileUpload.GetByteBuffer();
                    if (data != null && data.IsReadable())
                    {
                        diskFileUpload.AddContent((IByteBuffer)data.Retain(), false);
                    }
                    // release old upload
                    this.fileUpload.Release();

                    this.fileUpload = diskFileUpload;
                }
            }
            this.fileUpload.AddContent(buffer, last);
        }

        public void Delete() => this.fileUpload.Delete();

        public byte[] GetBytes() => this.fileUpload.GetBytes();

        public IByteBuffer GetByteBuffer() => this.fileUpload.GetByteBuffer();

        public Encoding Charset
        {
            get => this.fileUpload.Charset;
            set => this.fileUpload.Charset = value;
        }

        public string ContentType
        {
            get => this.fileUpload.ContentType;
            set => this.fileUpload.ContentType = value;
        }

        public string ContentTransferEncoding
        {
            get => this.fileUpload.ContentTransferEncoding;
            set => this.fileUpload.ContentTransferEncoding = value;
        }

        public string FileName
        {
            get => this.fileUpload.FileName;
            set => this.fileUpload.FileName = value;
        }

        public string GetString() => this.fileUpload.GetString();

        public string GetString(Encoding encoding) => this.fileUpload.GetString(encoding);

        public bool IsCompleted => this.fileUpload.IsCompleted;

        public bool IsInMemory => this.fileUpload.IsInMemory;

        public long Length => this.fileUpload.Length;

        public long DefinedLength => this.fileUpload.DefinedLength;

        public bool RenameTo(FileStream destination) => this.fileUpload.RenameTo(destination);

        public void SetContent(IByteBuffer buffer)
        {
            this.CheckSize(buffer.ReadableBytes);
            if (buffer.ReadableBytes > this.limitSize)
            {
                if (this.fileUpload is MemoryFileUpload memoryUpload)
                {
                    // change to Disk
                    this.fileUpload = new DiskFileUpload(
                        memoryUpload.Name, 
                        memoryUpload.FileName,
                        memoryUpload.ContentType,
                        memoryUpload.ContentTransferEncoding,
                        memoryUpload.Charset,
                        this.definedSize);
                    this.fileUpload.MaxSize = this.maxSize;

                    // release old upload
                    memoryUpload.Release();
                }
            }
            this.fileUpload.SetContent(buffer);
        }

        public void SetContent(Stream inputStream)
        {
            if (this.fileUpload is MemoryFileUpload)
            {
                IFileUpload memoryUpload = this.fileUpload;
                // change to Disk
                this.fileUpload = new DiskFileUpload(
                    this.fileUpload.Name,
                    this.fileUpload.FileName,
                    this.fileUpload.ContentType,
                    this.fileUpload.ContentTransferEncoding,
                    this.fileUpload.Charset,
                    this.definedSize);
                this.fileUpload.MaxSize = this.maxSize;

                // release old upload
                memoryUpload.Release();
            }
            this.fileUpload.SetContent(inputStream);
        }

        public HttpDataType DataType => this.fileUpload.DataType;

        public string Name => this.fileUpload.Name;

        // ReSharper disable once NonReadonlyMemberInGetHashCode
        public override int GetHashCode() => this.fileUpload.GetHashCode();

        public override bool Equals(object obj) => this.fileUpload.Equals(obj);

        public int CompareTo(IInterfaceHttpData other) => this.fileUpload.CompareTo(other);

        public override string ToString() => $"Mixed: {this.fileUpload}";

        public IByteBuffer GetChunk(int length) => this.fileUpload.GetChunk(length);

        public FileStream GetFile() => this.fileUpload.GetFile();

        public IByteBufferHolder Copy() => this.fileUpload.Copy();

        public IByteBufferHolder Duplicate() => this.fileUpload.Duplicate();

        public IByteBufferHolder RetainedDuplicate() => this.fileUpload.RetainedDuplicate();

        public IByteBufferHolder Replace(IByteBuffer content) => this.fileUpload.Replace(content);

        public IByteBuffer Content => this.fileUpload.Content;

        public int ReferenceCount => this.fileUpload.ReferenceCount;

        public IReferenceCounted Retain()
        {
            this.fileUpload.Retain();
            return this;
        }

        public IReferenceCounted Retain(int increment)
        {
            this.fileUpload.Retain(increment);
            return this;
        }

        public IReferenceCounted Touch()
        {
            this.fileUpload.Touch();
            return this;
        }

        public IReferenceCounted Touch(object hint)
        {
            this.fileUpload.Touch(hint);
            return this;
        }

        public bool Release() => this.fileUpload.Release();

        public bool Release(int decrement) => this.fileUpload.Release(decrement);
    }
}
