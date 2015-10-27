namespace DotNetty.Codecs.CoapTcp.util
{
    using System;
    using System.Collections.Generic;

    class BytesBuilder
    {
        private const int DEFAULT_BYTE_ARRAY_SIZE = 1024;
        private const int DEFAULT_BYTE_ARRAY_EXTENSION = 1024;

        private byte[] bytes = null;
        private int extensionFactor = 0;
        private int currentIndex = 0;

        private BytesBuilder(int initSize, int extensionFactor)
        {
            this.bytes = new byte[initSize];
            this.extensionFactor = extensionFactor;
        }

        public BytesBuilder Skip(int length)
        {
            this.currentIndex += length;
            return this;
        }

        public BytesBuilder AddInt(int value, int length, IntegerEncoding encoding)
        {
            PrepareBytesBuffer(currentIndex + length);
            BytesUtil.FromInt(value, length, encoding, bytes, currentIndex);
            currentIndex += length;
            return this;
        }

        public BytesBuilder AddByte(byte value)
        {
            PrepareBytesBuffer(currentIndex);
            bytes[currentIndex] = value;
            currentIndex += 1;
            return this;
        }

        public BytesBuilder AddBytes(byte[] value)
        {
            return AddBytes(value, value.Length);
        }

        public BytesBuilder AddBytes(byte[] value, int length, int offset=0)
        {
            PrepareBytesBuffer(currentIndex + length);
            Buffer.BlockCopy(value, offset, bytes, currentIndex, length);
            currentIndex += length;
            return this;
        }

        public BytesBuilder Add<T>(IEnumerable<T> values, IBytesBuildHelper<T> helper)
        {
            return helper.build(values, this);
        }

        public byte[] Build()
        {
            byte[] content = new byte[currentIndex];
            Buffer.BlockCopy(bytes, 0, content, 0, currentIndex);
            return content;
        }

        private void PrepareBytesBuffer(int offset)
        {
            if (bytes.Length <= offset)
            {
                // final offset goes beyond the byte array size
                int delta = offset - bytes.Length;
                int targetSize = (int)Math.Ceiling(delta * 1.0 / extensionFactor) * extensionFactor;

                byte[] newBytes = new byte[bytes.Length + targetSize];
                Buffer.BlockCopy(bytes, 0, newBytes, 0, bytes.Length);
                bytes = newBytes;
            }
        }

        public static BytesBuilder Create(int init=DEFAULT_BYTE_ARRAY_SIZE, int ext=DEFAULT_BYTE_ARRAY_EXTENSION)
        {
            return new BytesBuilder(init, ext);
        }
    }
}
