// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench.Buffers
{
    using System.Text;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Attributes.Jobs;
    using DotNetty.Buffers;
    using DotNetty.Common;

    [CoreJob]
    [BenchmarkCategory("ByteBuffer")]
    public class ByteBufUtilBenchmark
    {
        IByteBuffer buffer;
        IByteBuffer wrapped;
        IByteBuffer asciiBuffer;
        IByteBuffer utf8Buffer;

        string ascii;
        int asciiLength;
        string utf8;
        int utf8Length;

        static ByteBufUtilBenchmark()
        {
            ResourceLeakDetector.Level = ResourceLeakDetector.DetectionLevel.Disabled;
        }

        [GlobalSetup]
        public void GlobalSetup()
        {
            PooledByteBufferAllocator allocator = PooledByteBufferAllocator.Default;

            // Use buffer sizes that will also allow to write UTF-8 without grow the buffer
            this.buffer = allocator.DirectBuffer(512);
            this.wrapped = Unpooled.UnreleasableBuffer(allocator.DirectBuffer(512));
            var asciiSequence = new StringBuilder(128);
            for (int i = 0; i < 128; i++)
            {
                asciiSequence.Append('a');
            }
            this.ascii = asciiSequence.ToString();

            // Generate some mixed UTF-8 String for benchmark
            var utf8Sequence = new StringBuilder(128);
            char[] chars = "Some UTF-8 like äÄ∏ŒŒ".ToCharArray();
            for (int i = 0; i < 128; i++)
            {
                utf8Sequence.Append(chars[i % chars.Length]);
            }
            this.utf8 = utf8Sequence.ToString();

            byte[] bytes = Encoding.ASCII.GetBytes(this.ascii);
            this.asciiLength = bytes.Length;
            this.asciiBuffer = allocator.DirectBuffer(this.asciiLength);
            this.asciiBuffer.WriteBytes(bytes);

            bytes = Encoding.UTF8.GetBytes(this.utf8);
            this.utf8Length = bytes.Length;
            this.utf8Buffer = allocator.DirectBuffer(bytes.Length);
            this.utf8Buffer.WriteBytes(bytes);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.buffer.Release();
            this.wrapped.Release();
            this.asciiBuffer.Release();
            this.utf8Buffer.Release();
        }

        [Benchmark]
        public string GetAsciiString() => this.asciiBuffer.GetString(0, this.asciiLength, Encoding.ASCII);

        [Benchmark]
        public string GetUtf8String() => this.utf8Buffer.GetString(0, this.utf8Length, Encoding.UTF8);

        [Benchmark]
        public void WriteAsciiStringViaArray()
        {
            this.buffer.ResetWriterIndex();
            this.buffer.WriteBytes(Encoding.ASCII.GetBytes(this.ascii));
        }

        [Benchmark]
        public void WriteAsciiStringViaArrayWrapped()
        {
            this.wrapped.ResetWriterIndex();
            this.wrapped.WriteBytes(Encoding.ASCII.GetBytes(this.ascii));
        }

        [Benchmark]
        public void WriteAsciiString()
        {
            this.buffer.ResetWriterIndex();
            this.buffer.WriteString(this.ascii, Encoding.ASCII);
        }

        [Benchmark]
        public void WriteAsciiStringWrapped()
        {
            this.wrapped.ResetWriterIndex();
            this.wrapped.WriteString(this.ascii, Encoding.ASCII);
        }

        [Benchmark]
        public void WriteUtf8StringViaArray()
        {
            this.buffer.ResetWriterIndex();
            this.buffer.WriteBytes(Encoding.UTF8.GetBytes(this.utf8));
        }

        [Benchmark]
        public void WriteUtf8StringViaArrayWrapped()
        {
            this.wrapped.ResetWriterIndex();
            this.wrapped.WriteBytes(Encoding.UTF8.GetBytes(this.utf8));
        }

        [Benchmark]
        public void WriteUtf8String()
        {
            this.buffer.ResetWriterIndex();
            this.buffer.WriteString(this.utf8, Encoding.UTF8);
        }

        [Benchmark]
        public void WriteUtf8StringWrapped()
        {
            this.wrapped.ResetWriterIndex();
            this.wrapped.WriteString(this.utf8, Encoding.UTF8);
        }
    }
}
