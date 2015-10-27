namespace DotNetty.Codecs.CoapTcp.util
{
    using System;

    class BytesReader
    {
        private byte[] bytes;
        private int currentIndex;
        private BytesReader(byte[] bytes, int initIndex)
        {
            this.bytes = bytes;
            this.currentIndex = initIndex;
        }

        public byte ReadByte()
        {
            byte b = bytes[currentIndex];
            currentIndex += 1;
            return b;
        }

        public byte[] ReadBytes(int size)
        {
            byte[] b = new byte[size];
            Buffer.BlockCopy(bytes, currentIndex, b, 0, size);
            currentIndex += size;
            return b;
        }

        public int ReadInt(int size, IntegerEncoding encoding)
        {
            byte[] b = ReadBytes(size);
            return BytesUtil.ToInt(b, size, encoding);
        }

        public int GetNumBytesRead()
        {
            return currentIndex;
        }

        public static BytesReader Create(byte[] bytes, int offset = 0)
        {
            return new BytesReader(bytes, offset);
        }
    }
}
