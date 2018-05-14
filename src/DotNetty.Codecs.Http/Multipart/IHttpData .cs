// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Multipart
{
    using System.IO;
    using System.Text;
    using DotNetty.Buffers;

    public interface IHttpData : IInterfaceHttpData, IByteBufferHolder
    {
        long MaxSize { get; set; }

        void CheckSize(long newSize);

        void SetContent(IByteBuffer buffer);

        void SetContent(Stream source);

        void AddContent(IByteBuffer buffer, bool last);

        bool IsCompleted { get; }

        long Length { get; }

        long DefinedLength { get; }

        void Delete();

        byte[] GetBytes();

        IByteBuffer GetByteBuffer();

        IByteBuffer GetChunk(int length);

        string GetString();

        string GetString(Encoding charset);

        Encoding Charset { get; set; }

        bool RenameTo(FileStream destination);

        bool IsInMemory { get; }

        FileStream GetFile();
    }
}
