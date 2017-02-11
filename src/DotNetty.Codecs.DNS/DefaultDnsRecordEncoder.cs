using System;
using System.Text;
using DotNetty.Buffers;
using DotNetty.Codecs.DNS.Records;
using System.Net.Sockets;
using DotNetty.Common.Utilities;

namespace DotNetty.Codecs.DNS
{
    public class DefaultDnsRecordEncoder : IDnsRecordEncoder
    {
        private const int PREFIX_MASK = sizeof(byte) - 1;
        private const string ROOT = ".";

        internal DefaultDnsRecordEncoder() { }

        public void EncodeQuestion(IDnsQuestion question, IByteBuffer output)
        {
            EncodeName(question.Name, output);
            output.WriteShort(question.Type.IntValue);
            output.WriteShort((int)question.DnsClass);
        }

        public void EncodeRecord(IDnsRecord record, IByteBuffer output)
        {
            if (record is IDnsQuestion)
            {
                EncodeQuestion((IDnsQuestion)record, output);
            }
            else if (record is IDnsPtrRecord)
            {
                EncodePtrRecord((IDnsPtrRecord)record, output);
            }
            else if (record is IDnsOptEcsRecord)
            {
                EncodeOptEcsRecord((IDnsOptEcsRecord)record, output);
            }
            else if (record is IDnsOptPseudoRecord)
            {
                EncodeOptPseudoRecord((IDnsOptPseudoRecord)record, output);
            }
            else if (record is IDnsRawRecord)
            {
                EncodeRawRecord((IDnsRawRecord)record, output);
            }
            else
            {
                throw new UnsupportedMessageTypeException(record.Type.Name);
            }
        }

        private void EncodeRawRecord(IDnsRawRecord record, IByteBuffer output)
        {
            EncodeRecordCore(record, output);

            IByteBuffer content = record.Content;
            int contentLen = content.ReadableBytes;
            output.WriteShort(contentLen);
            output.WriteBytes(content, content.ReaderIndex, contentLen);
        }

        private void EncodeOptPseudoRecord(IDnsOptPseudoRecord record, IByteBuffer output)
        {
            EncodeRecordCore(record, output);
            output.WriteShort(0);
        }

        private void EncodeOptEcsRecord(IDnsOptEcsRecord record, IByteBuffer output)
        {
            EncodeRecordCore(record, output);

            int sourcePrefixLength = record.SourcePrefixLength;
            int scopePrefixLength = record.ScopePrefixLength;
            int lowOrderBitsToPreserve = sourcePrefixLength & PREFIX_MASK;

            byte[] bytes = record.Address;
            int addressBits = bytes.Length << 3;
            if (addressBits < sourcePrefixLength || sourcePrefixLength < 0)
                throw new ArgumentException($"{sourcePrefixLength}: {sourcePrefixLength} (expected 0 >= {addressBits})");

            short addressNumber = (short)(bytes.Length == 4 ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6);
            int payloadLength = CalculateEcsAddressLength(sourcePrefixLength, lowOrderBitsToPreserve);
            int fullPayloadLength = 2 + 2 + 2 + 1 + 1 + payloadLength;

            output.WriteShort(fullPayloadLength);
            output.WriteShort(8);
            output.WriteShort(fullPayloadLength - 4);
            output.WriteShort(addressNumber);
            output.WriteByte(sourcePrefixLength);
            output.WriteByte(scopePrefixLength);

            if (lowOrderBitsToPreserve > 0)
            {
                int bytesLength = payloadLength - 1;
                output.WriteBytes(bytes, 0, bytesLength);
                output.WriteByte(PadWithZeros(bytes[bytesLength], lowOrderBitsToPreserve));
            }
            else
            {
                output.WriteBytes(bytes, 0, payloadLength);
            }
        }

        private static int CalculateEcsAddressLength(int sourcePrefixLength, int lowOrderBitsToPreserve)
        {
            return sourcePrefixLength.RightUShift(3) + (lowOrderBitsToPreserve != 0 ? 1 : 0);
        }

        private void EncodePtrRecord(IDnsPtrRecord record, IByteBuffer output)
        {
            EncodeRecordCore(record, output);
            EncodeName(record.HostName, output);
        }

        private void EncodeRecordCore(IDnsRecord record, IByteBuffer output)
        {
            EncodeName(record.Name, output);
            output.WriteShort(record.Type.IntValue);
            output.WriteShort((int)record.DnsClass);
            output.WriteInt((int)record.TimeToLive);
        }

        protected void EncodeName(string name, IByteBuffer buffer)
        {
            if (ROOT.Equals(name))
            {
                buffer.WriteByte(0);
                return;
            }

            string[] labels = name.Split('.');
            foreach (var label in labels)
            {
                int labelLen = label.Length;
                if (labelLen == 0)
                    break;

                buffer.WriteByte(labelLen);
                buffer.WriteBytes(Encoding.UTF8.GetBytes(label)); //TODO: Use ByteBufferUtil.WriteAscii() when available
            }
            buffer.WriteByte(0);
        }

        private static byte PadWithZeros(byte b, int lowOrderBitsToPreserve)
        {
            switch (lowOrderBitsToPreserve)
            {
                case 0:
                    return 0;
                case 1:
                    return (byte)(0x01 & b);
                case 2:
                    return (byte)(0x03 & b);
                case 3:
                    return (byte)(0x07 & b);
                case 4:
                    return (byte)(0x0F & b);
                case 5:
                    return (byte)(0x1F & b);
                case 6:
                    return (byte)(0x3F & b);
                case 7:
                    return (byte)(0x7F & b);
                case 8:
                    return b;
                default:
                    throw new ArgumentException($"lowOrderBitsToPreserve: {lowOrderBitsToPreserve}");
            }
        }
    }
}
