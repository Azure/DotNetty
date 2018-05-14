// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Redis.Messages
{
    using DotNetty.Buffers;

    /// <summary>
    /// Type of RESP (Redis Serialization Protocol)
    /// see http://redis.io/topics/protocol
    /// </summary>
    public sealed class RedisMessageType
    {
        public static readonly RedisMessageType InlineCommand = new RedisMessageType(0, true);
        public static readonly RedisMessageType SimpleString = new RedisMessageType((byte)'+', true);
        public static readonly RedisMessageType Error = new RedisMessageType((byte)'-', true);
        public static readonly RedisMessageType Integer = new RedisMessageType((byte)':', true);
        public static readonly RedisMessageType BulkString = new RedisMessageType((byte)'$', false);
        public static readonly RedisMessageType ArrayHeader = new RedisMessageType((byte)'*', false);

        readonly byte value;
        readonly bool inline;

        RedisMessageType(byte value, bool inline)
        {
            this.value = value;
            this.inline = inline;
        }

        public int Length => this.value > 0 ? RedisConstants.TypeLength : 0;

        public bool Inline => this.inline;

        public static RedisMessageType ReadFrom(IByteBuffer input, bool decodeInlineCommands)
        {
            int initialIndex = input.ReaderIndex;
            RedisMessageType type = ValueOf(input.ReadByte());
            if (type == InlineCommand)
            {
                if (!decodeInlineCommands)
                {
                    throw new RedisCodecException("Decoding of inline commands is disabled");
                }
                // reset index to make content readable again
                input.SetReaderIndex(initialIndex);
            }
            return type;
        }

        public void WriteTo(IByteBuffer output)
        {
            if (this.value == 0) // InlineCommand
            {
                return;
            }
            output.WriteByte(this.value);
        }

        static RedisMessageType ValueOf(byte value)
        {
            switch (value)
            {
                case (byte)'+':
                    return SimpleString;
                case (byte)'-':
                    return Error;
                case (byte)':':
                    return Integer;
                case (byte)'$':
                    return BulkString;
                case (byte)'*':
                    return ArrayHeader;
                default:
                    return InlineCommand;
            }
        }
    }
}