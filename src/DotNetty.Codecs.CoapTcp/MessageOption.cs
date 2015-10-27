namespace DotNetty.Codecs.CoapTcp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DotNetty.Codecs.CoapTcp.util;

    public class MessageOption
    {
        public enum Name
        {
            Reserved = 0,
            If_Match = 1,
            Uri_Host = 3,
            ETag = 4,
            If_None_Match = 5,
            Observe = 6,
            Uri_Port = 7,
            Location_Path = 8,
            Uri_Path = 11,
            Content_Format = 12,
            Max_Age = 14,
            Uri_Query = 15,
            Accept = 17,
            Location_Query = 20,
            Proxy_Uri = 35,
            Proxy_Scheme = 39,
            Size1= 60
        };

        public enum DataType
        {
            EMPTY,
            OPAQUE,
            UINT,
            STRING
        };

        public const byte END_OF_OPTIONS = 0xFF;

        private static Dictionary<int, DataType> OPTION_NUMBER_TO_OPTION_DATA_TYPE =
            new Dictionary<int, DataType>() {
                {0 /* reserved */, DataType.EMPTY},
                {1 /* if-match */, DataType.OPAQUE},
                {3 /* uri-host */, DataType.STRING},
                {4 /* etag */, DataType.OPAQUE},
                {5 /* if-none-match */, DataType.EMPTY},
                {6 /* observe */, DataType.UINT},
                {7 /* uri-port */, DataType.UINT},
                {8 /* location-path */, DataType.STRING},
                {11 /* uri-path */, DataType.STRING},
                {12 /* content-format */, DataType.UINT},
                {14 /* max-age */, DataType.UINT},
                {15 /* uri-query */, DataType.STRING},
                {17 /* accept */, DataType.UINT},
                {20 /* location-query */, DataType.STRING},
                {35 /* proxy-uri */, DataType.STRING},
                {39 /* proxy-scheme */, DataType.STRING},
                {60 /* size1 */, DataType.UINT},
                {128 /* reserved */, DataType.EMPTY},
                {132 /* reserved */, DataType.EMPTY},
                {136 /* reserved */, DataType.EMPTY},
                {140 /* reserved */, DataType.EMPTY}
            };
        private static Dictionary<int, Name> OPTION_NUMBER_TO_OPTION_NAME =
            new Dictionary<int, Name>() { 
                {1, Name.If_Match},
                {3, Name.Uri_Host},
                {4, Name.ETag},
                {5, Name.If_None_Match},
                {6, Name.Observe},
                {7, Name.Uri_Port},
                {8, Name.Location_Path},
                {11, Name.Uri_Path},
                {12, Name.Content_Format},
                {14, Name.Max_Age},
                {15, Name.Uri_Query},
                {17, Name.Accept},
                {20, Name.Location_Query},
                {35, Name.Proxy_Uri},
                {39, Name.Proxy_Scheme},
                {60, Name.Size1}
            };

        public int OptionNumber { get { return optionNumber; } }
        public int OptionLength { get { return optionLength; } }
        public byte[] Payload { get { return payload; } }
        public Name OptionName { get { return GetName(optionNumber); } }
        public DataType OptionType { get { return GetType(optionNumber); } }

        private int optionNumber;
        private int optionLength;
        private byte[] payload;

        private MessageOption(int optionNumber, int optionLength, byte[] payload)
        {
            this.optionNumber = optionNumber;
            this.optionLength = optionLength;
            this.payload = payload;
        }

        public int GetIntValue()
        {
            DataType type = GetType(optionNumber);
            if (type == DataType.UINT)
            {
                return BytesUtil.ToInt(payload, Math.Min(optionLength, 4), IntegerEncoding.NETWORK_ORDER);
            }
            throw new ArgumentException("the payload is not of integer type; optionNumber: "
                + optionNumber + "; expected payload type: " + type.ToString());
        }

        public byte[] GetBytes()
        {
            DataType type = GetType(optionNumber);
            if (type == DataType.OPAQUE)
            {
                return payload;
            }
            throw new ArgumentException("the payload is not of opaque type; optionNumber: "
                + optionNumber + "; expected payload type: " + type.ToString());
        }

        public string GetString()
        {
            DataType type = GetType(optionNumber);
            if (type == DataType.STRING)
            {
                return BytesUtil.ToUTF8String(payload, payload.Length);
            }
            throw new ArgumentException("the payload is not of string type; optionNumber: "
                + optionNumber + "; expected payload type: " + type.ToString());
        }

        public byte[] ToByteArray(int previousOptionNumber = 0)
        {
            Tuple<byte, int, int> encodedDelta = VariableLengthIntegerCodec.Encode(optionNumber - previousOptionNumber);
            Tuple<byte, int, int> encodedLength = VariableLengthIntegerCodec.Encode(optionLength);

            // the first byte is composed of 4-bit delta
            byte optionHeader = (byte)((encodedDelta.Item1 + encodedLength.Item1 << 4));

            BytesBuilder builder = BytesBuilder.Create();
            return builder.AddByte(optionHeader)
                .AddInt(encodedDelta.Item2, encodedDelta.Item3, IntegerEncoding.NETWORK_ORDER)
                .AddInt(encodedLength.Item2, encodedLength.Item3, IntegerEncoding.NETWORK_ORDER)
                .AddBytes(payload, payload.Length)
                .Build();
        }

        public override bool Equals(Object obj)
        {
            if (null == obj || GetType() != obj.GetType())
            {
                return false;
            }

            MessageOption messageOption = (MessageOption)obj;
            return
                OptionNumber == messageOption.optionNumber &&
                OptionLength == messageOption.OptionLength &&
                Payload.SequenceEqual(messageOption.Payload);
        }

        public override int GetHashCode()
        {
            int baseHashCode = base.GetHashCode();
            int payload0 = Payload.Length > 0 ? Payload[0] : 0;
            return baseHashCode + OptionNumber + OptionLength + Payload[0];
        }

        public static MessageOption Create(int optionNumber, int optionLength, byte[] payload)
        {
            return new MessageOption(optionNumber, optionLength, payload);
        }

        public static Name GetName(int optionNumber)
        {
            Name name = Name.Reserved;
            OPTION_NUMBER_TO_OPTION_NAME.TryGetValue(optionNumber, out name);
            return name;
        }

        public static DataType GetType(int optionNumber)
        {
            DataType type = DataType.EMPTY;
            OPTION_NUMBER_TO_OPTION_DATA_TYPE.TryGetValue(optionNumber, out type);
            return type;
        }
    }
}
