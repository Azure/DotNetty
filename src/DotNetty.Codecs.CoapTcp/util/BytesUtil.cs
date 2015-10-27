namespace DotNetty.Codecs.CoapTcp.util
{
    using System;
    using System.Text;

    class BytesUtil
    {
        public static int ToInt(byte[] b, int size, IntegerEncoding encoding)
        {
            int v = 0;
            switch (encoding)
            {
                case IntegerEncoding.NETWORK_ORDER:
                    for (int i = 0; i < size; i++)
                    {
                        v = (v << 8) + b[i];
                    }
                    break;
                default:
                    throw new ArgumentException("unexpected encoding scheme: " + encoding);
            }
            return v;
        }

        public static string ToUTF8String(byte[] b, int length)
        {
            return ToString(b, length, Encoding.UTF8);
        }

        public static string ToString(byte[] b, int size, Encoding encodeScheme)
        {
            return encodeScheme.GetString(b, 0, size);
        }

        public static byte[] FromInt(int v, int size, IntegerEncoding encoding, byte[] b, int offset = 0)
        {
            if (null == b)
            {
                b = new byte[size];
            }

            switch (encoding)
            {
                case IntegerEncoding.NETWORK_ORDER:
                    for (int i = size - 1; i >= 0; i--)
                    {
                        b[offset + i] = (byte)v;
                        v = v >> 8;
                    }
                    break;
                default:
                    throw new ArgumentException("unexpected encoding scheme: " + encoding);
            }
            
            return b;
        }
    }
}
