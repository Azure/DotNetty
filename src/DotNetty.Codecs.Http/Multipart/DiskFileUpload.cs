// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Http.Multipart
{
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    public class DiskFileUpload : AbstractDiskHttpData, IFileUpload
    {
        public static string FileBaseDirectory;
        public static bool DeleteOnExitTemporaryFile = true;
        public static string FilePrefix = "FUp_";
        public static readonly string FilePostfix = ".tmp";

        string filename;
        string contentType;
        string contentTransferEncoding;

        public DiskFileUpload(string name, string filename, string contentType, 
            string contentTransferEncoding, Encoding charset, long size)
            : base(name, charset, size)
        {
            Contract.Requires(filename != null);
            Contract.Requires(contentType != null);

            this.filename = filename;
            this.contentType = contentType;
            this.contentTransferEncoding = contentTransferEncoding;
        }

        public override HttpDataType DataType => HttpDataType.FileUpload;

        public string FileName
        {
            get => this.filename;
            set
            {
                Contract.Requires(value != null);
                this.filename = value;
            }
        }

        public override int GetHashCode() => FileUploadUtil.HashCode(this);

        public override bool Equals(object obj) => obj is IFileUpload fileUpload && FileUploadUtil.Equals(this, fileUpload);

        public override int CompareTo(IInterfaceHttpData other)
        {
            if (!(other is IFileUpload))
            {
                throw new ArgumentException($"Cannot compare {this.DataType}  with {other.DataType}");
            }

            return this.CompareTo((IFileUpload)other);
        }

        public int CompareTo(IFileUpload other) => FileUploadUtil.CompareTo(this, other);

        public string ContentType
        {
            get => this.contentType;
            set
            {
                Contract.Requires(value != null);
                this.contentType = value;
            }
        }

        public string ContentTransferEncoding
        {
            get => this.contentTransferEncoding;
            set => this.contentTransferEncoding = value;
        }

        public override string ToString()
        {
            FileStream fileStream = null;
            try
            {
                fileStream = this.GetFile();
            }
            catch (IOException)
            {
                // Should not occur.
            }

            return HttpHeaderNames.ContentDisposition  + ": " +
               HttpHeaderValues.FormData + "; " + HttpHeaderValues.Name + "=\"" + this.Name +
                "\"; " + HttpHeaderValues.FileName + "=\"" + this.filename + "\"\r\n" +
                HttpHeaderNames.ContentType + ": " + this.contentType +
                (this.Charset != null ? "; " + HttpHeaderValues.Charset + '=' + this.Charset.WebName + "\r\n" : "\r\n") +
                HttpHeaderNames.ContentLength + ": " + this.Length + "\r\n" +
                "Completed: " + this.IsCompleted +
                "\r\nIsInMemory: " + this.IsInMemory + "\r\nRealFile: " +
                (fileStream != null ? fileStream.Name : "null") + " DefaultDeleteAfter: " +
                DeleteOnExitTemporaryFile;
        }

        protected override bool DeleteOnExit => DeleteOnExitTemporaryFile;

        protected override string BaseDirectory => FileBaseDirectory;

        protected override string DiskFilename => "upload";

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
                    var duplicate = (IFileUpload)this.Replace(content);
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
            var upload = new DiskFileUpload(
                this.Name, this.FileName, this.ContentType, this.ContentTransferEncoding, this.Charset, this.Size);
            if (content != null)
            {
                try
                {
                    upload.SetContent(content);
                }
                catch (IOException e)
                {
                    throw new ChannelException(e);
                }
            }

            return upload;
        }
    }
}
