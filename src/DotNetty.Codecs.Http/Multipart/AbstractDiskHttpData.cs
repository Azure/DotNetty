// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Multipart
{
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    public abstract class AbstractDiskHttpData : AbstractHttpData
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<AbstractDiskHttpData>();

        FileStream fileStream;

        protected AbstractDiskHttpData(string name, Encoding charset, long size) : base(name, charset, size)
        {
        }

        protected abstract string DiskFilename { get; }

        protected abstract string Prefix { get; }

        protected abstract string BaseDirectory { get; }

        protected abstract string Postfix { get; }

        protected abstract bool DeleteOnExit { get; }

        FileStream TempFile()
        {
            string newpostfix;
            string diskFilename = this.DiskFilename;
            if (diskFilename != null)
            {
                newpostfix = '_' + diskFilename;
            }
            else
            {
                newpostfix = this.Postfix;
            }
            string directory = this.BaseDirectory == null 
                ? Path.GetTempPath() 
                : Path.Combine(Path.GetTempPath(), this.BaseDirectory);
            // File.createTempFile
            string fileName = Path.Combine(directory, $"{this.Prefix}{Path.GetRandomFileName()}{newpostfix}");
            FileStream tmpFile = File.Create(fileName, 4096, // DefaultBufferSize
                this.DeleteOnExit ? FileOptions.DeleteOnClose : FileOptions.None);
            return tmpFile;
        }

        public override void SetContent(IByteBuffer buffer)
        {
            Contract.Requires(buffer != null);
            try
            {
                this.Size = buffer.ReadableBytes;
                this.CheckSize(this.Size);
                if (this.DefinedSize > 0 && this.DefinedSize < this.Size)
                {
                    throw new IOException($"Out of size: {this.Size} > {this.DefinedSize}");
                }
                if (this.fileStream == null)
                {
                    this.fileStream = this.TempFile();
                }
                if (buffer.ReadableBytes == 0)
                {
                    // empty file
                    return;
                }

                buffer.GetBytes(buffer.ReaderIndex, this.fileStream, buffer.ReadableBytes);
                buffer.SetReaderIndex(buffer.ReaderIndex + buffer.ReadableBytes);
                this.fileStream.Flush();
                this.SetCompleted();
            }
            finally
            {
                // Release the buffer as it was retained before and we not need a reference to it at all
                // See https://github.com/netty/netty/issues/1516
                buffer.Release();
            }
        }

        public override void AddContent(IByteBuffer buffer, bool last)
        {
            if (buffer != null)
            {
                try
                {
                    int localsize = buffer.ReadableBytes;
                    this.CheckSize(this.Size + localsize);
                    if (this.DefinedSize > 0 && this.DefinedSize < this.Size + localsize)
                    {
                        throw new IOException($"Out of size: {this.Size} > {this.DefinedSize}");
                    }
                    if (this.fileStream == null)
                    {
                        this.fileStream = this.TempFile();
                    }
                    buffer.GetBytes(buffer.ReaderIndex, this.fileStream, buffer.ReadableBytes);
                    buffer.SetReaderIndex(buffer.ReaderIndex + localsize);
                    this.fileStream.Flush();

                    this.Size += buffer.ReadableBytes;
                }
                finally
                {
                    // Release the buffer as it was retained before and we not need a reference to it at all
                    // See https://github.com/netty/netty/issues/1516
                    buffer.Release();
                }
            }
            if (last)
            {
                if (this.fileStream == null)
                {
                    this.fileStream = this.TempFile();
                }
                this.SetCompleted();
            }
            else
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer));
                }
            }
        }

        public override void SetContent(Stream source)
        {
            Contract.Requires(source != null);

            if (this.fileStream != null)
            {
                this.Delete();
            }

            this.fileStream = this.TempFile();
            int written = 0;
            var bytes = new byte[4096 * 4];
            while (true)
            {
                int read = source.Read(bytes, 0, bytes.Length);
                if (read <= 0)
                {
                    break;
                }

                written += read;
                this.CheckSize(written);
                this.fileStream.Write(bytes, 0, read);
            }
            this.fileStream.Flush();
            // Reset the position to start for reads
            this.fileStream.Position -= written;

            this.Size = written;
            if (this.DefinedSize > 0 && this.DefinedSize < this.Size)
            {
                try
                {
                    Delete(this.fileStream);
                }
                catch (Exception error)
                {
                    Logger.Warn("Failed to delete: {} {}", this.fileStream, error);
                }
                this.fileStream = null;
                throw new IOException($"Out of size: {this.Size} > {this.DefinedSize}");
            }
            //isRenamed = true;
            this.SetCompleted();
        }

        public override void Delete()
        {
            if (this.fileStream != null)
            {
                try
                {
                    Delete(this.fileStream);
                }
                catch (IOException error)
                {
                    Logger.Warn("Failed to delete file.", error);
                }

                this.fileStream = null;
            }
        }

        public override byte[] GetBytes() => this.fileStream == null 
            ? ArrayExtensions.ZeroBytes : ReadFrom(this.fileStream);

        public override IByteBuffer GetByteBuffer()
        {
            if (this.fileStream == null)
            {
                return Unpooled.Empty;
            }

            byte[] array = ReadFrom(this.fileStream);
            return Unpooled.WrappedBuffer(array);
        }

        public override IByteBuffer GetChunk(int length)
        {
            if (this.fileStream == null || length == 0)
            {
                return Unpooled.Empty;
            }
            int read = 0;
            var bytes = new byte[length];
            while (read < length)
            {
                int readnow = this.fileStream.Read(bytes, read, length - read);
                if (readnow <= 0)
                {
                    break;
                }

                read += readnow;
            }
            if (read == 0)
            {
                return Unpooled.Empty;
            }
            IByteBuffer buffer = Unpooled.WrappedBuffer(bytes);
            buffer.SetReaderIndex(0);
            buffer.SetWriterIndex(read);
            return buffer;
        }

        public override string GetString() => this.GetString(HttpConstants.DefaultEncoding);

        public override string GetString(Encoding encoding)
        {
            if (this.fileStream == null)
            {
                return string.Empty;
            }
            byte[] array = ReadFrom(this.fileStream);
            if (encoding == null)
            {
                encoding = HttpConstants.DefaultEncoding;
            }

            return encoding.GetString(array);
        }

        public override bool IsInMemory => false;

        public override bool RenameTo(FileStream destination)
        {
            Contract.Requires(destination != null);
            if (this.fileStream == null)
            {
                throw new InvalidOperationException("No file defined so cannot be renamed");
            }

            // must copy
            long chunkSize = 8196;
            int position = 0;
            while (position < this.Size)
            {
                if (chunkSize < this.Size - position)
                {
                    chunkSize = this.Size - position;
                }

                var buffer = new byte[chunkSize];
                int read = this.fileStream.Read(buffer, 0, (int)chunkSize);
                if (read <= 0)
                {
                    break;
                }

                destination.Write(buffer, 0, read);
                position += read;
            }

            if (position == this.Size)
            {
                try
                {
                    Delete(this.fileStream);
                }
                catch (IOException exception)
                {
                    Logger.Warn("Failed to delete file.", exception);
                }
                this.fileStream = destination;
                return true;
            }
            else
            {
                try
                {
                    Delete(destination);
                }
                catch (IOException exception)
                {
                    Logger.Warn("Failed to delete file.", exception);
                }
                return false;
            }
        }

        static void Delete(FileStream fileStream)
        {
            string fileName = fileStream.Name;
            fileStream.Dispose();
            File.Delete(fileName);
        }

        static byte[] ReadFrom(Stream fileStream)
        {
            long srcsize = fileStream.Length;
            if (srcsize > int.MaxValue)
            {
                throw new ArgumentException("File too big to be loaded in memory");
            }

            var array = new byte[(int)srcsize];
            fileStream.Read(array, 0, array.Length);
            return array;
        }

        public override FileStream GetFile() => this.fileStream;

        public override IReferenceCounted Touch(object hint) => this;
    }
}
