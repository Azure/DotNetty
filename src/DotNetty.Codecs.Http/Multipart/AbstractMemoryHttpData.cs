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

    public abstract class AbstractMemoryHttpData : AbstractHttpData
    {
        IByteBuffer byteBuf;
        int chunkPosition;

        protected AbstractMemoryHttpData(string name, Encoding charset, long size) 
            : base(name, charset, size)
        {
        }

        public override void SetContent(IByteBuffer buffer)
        {
            Contract.Requires(buffer != null);

            long localsize = buffer.ReadableBytes;
            this.CheckSize(localsize);
            if (this.DefinedSize > 0 && this.DefinedSize < localsize)
            {
                throw new IOException($"Out of size: {localsize} > {this.DefinedSize}");
            }
            this.byteBuf?.Release();

            this.byteBuf = buffer;
            this.Size = localsize;
            this.SetCompleted();
        }

        public override void SetContent(Stream inputStream)
        {
            Contract.Requires(inputStream != null);

            if (!inputStream.CanRead)
            {
                throw new ArgumentException($"{nameof(inputStream)} is not readable");
            }

            IByteBuffer buffer = Unpooled.Buffer();
            var bytes = new byte[4096 * 4];
            int written = 0;
            while (true)
            {
                int read = inputStream.Read(bytes, 0, bytes.Length);
                if (read <= 0)
                {
                    break;
                }

                buffer.WriteBytes(bytes, 0, read);
                written += read;
                this.CheckSize(written);
            }
            this.Size = written;
            if (this.DefinedSize > 0 && this.DefinedSize < this.Size)
            {
                throw new IOException($"Out of size: {this.Size} > {this.DefinedSize}");
            }

            this.byteBuf?.Release();
            this.byteBuf = buffer;
            this.SetCompleted();
        }

        public override void AddContent(IByteBuffer buffer, bool last)
        {
            if (buffer != null)
            {
                long localsize = buffer.ReadableBytes;
                this.CheckSize(this.Size + localsize);
                if (this.DefinedSize > 0 && this.DefinedSize < this.Size + localsize)
                {
                    throw new IOException($"Out of size: {(this.Size + localsize)} > {this.DefinedSize}");
                }

                this.Size += localsize;
                if (this.byteBuf == null)
                {
                    this.byteBuf = buffer;
                }
                else if (this.byteBuf is CompositeByteBuffer buf)
                {
                    buf.AddComponent(true, buffer);
                    buf.SetWriterIndex((int)this.Size);
                }
                else
                {
                    CompositeByteBuffer compositeBuffer = Unpooled.CompositeBuffer(int.MaxValue);
                    compositeBuffer.AddComponents(true, this.byteBuf, buffer);
                    compositeBuffer.SetWriterIndex((int)this.Size);
                    this.byteBuf = compositeBuffer;
                }
            }
            if (last)
            {
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

        public override void Delete()
        {
            if (this.byteBuf != null)
            {
                this.byteBuf.Release();
                this.byteBuf = null;
            }
        }

        public override byte[] GetBytes()
        {
            if (this.byteBuf == null)
            {
                return Unpooled.Empty.Array;
            }

            var array = new byte[this.byteBuf.ReadableBytes];
            this.byteBuf.GetBytes(this.byteBuf.ReaderIndex, array);
            return array;
        }

        public override string GetString() => this.GetString(HttpConstants.DefaultEncoding);

        public override string GetString(Encoding encoding)
        {
            if (this.byteBuf == null)
            {
                return string.Empty;
            }
            if (encoding == null)
            {
                encoding = HttpConstants.DefaultEncoding;
            }
            return this.byteBuf.ToString(encoding);
        }

        public override IByteBuffer GetByteBuffer() => this.byteBuf;

        public override IByteBuffer GetChunk(int length)
        {
            if (this.byteBuf == null || length == 0 || this.byteBuf.ReadableBytes == 0)
            {
                this.chunkPosition = 0;
                return Unpooled.Empty;
            }
            int sizeLeft = this.byteBuf.ReadableBytes - this.chunkPosition;
            if (sizeLeft == 0)
            {
                this.chunkPosition = 0;
                return Unpooled.Empty;
            }
            int sliceLength = length;
            if (sizeLeft < length)
            {
                sliceLength = sizeLeft;
            }

            IByteBuffer chunk = this.byteBuf.RetainedSlice(this.chunkPosition, sliceLength);
            this.chunkPosition += sliceLength;
            return chunk;
        }

        public override bool IsInMemory => true;

        public override bool RenameTo(FileStream destination)
        {
            Contract.Requires(destination != null);

            if (!destination.CanWrite)
            {
                throw new ArgumentException($"{nameof(destination)} is not writable");
            }
            if (this.byteBuf == null)
            {
                return true;
            }

            this.byteBuf.GetBytes(this.byteBuf.ReaderIndex, destination, this.byteBuf.ReadableBytes);
            destination.Flush();
            return true;
        }

        public override FileStream GetFile() => throw new IOException("Not represented by a stream");

        public override IReferenceCounted Touch(object hint)
        {
            this.byteBuf?.Touch(hint);
            return this;
        }
    }
}
