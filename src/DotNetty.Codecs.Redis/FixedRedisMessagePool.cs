// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis
{
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Redis.Messages;

    public sealed class FixedRedisMessagePool : IRedisMessagePool
    {
        static readonly string[] DefaultSimpleStrings =
        {
            "OK",
            "PONG",
            "QUEUED"
        };

        static readonly string[] DefaultErrors =
        {
            "ERR",
            "ERR index out of range",
            "ERR no such key",
            "ERR source and destination objects are the same",
            "ERR syntax error",
            "BUSY Redis is busy running a script. You can only call SCRIPT KILL or SHUTDOWN NOSAVE.",
            "BUSYKEY Target key name already exists.",
            "EXECABORT Transaction discarded because of previous errors.",
            "LOADING Redis is loading the dataset in memory",
            "MASTERDOWN Link with MASTER is down and slave-serve-stale-data is set to 'no'.",
            "MISCONF Redis is configured to save RDB snapshots, but is currently not able to persist on disk. " +
            "Commands that may modify the data set are disabled. Please check Redis logs for details " +
            "about the error.",
            "NOAUTH Authentication required.",
            "NOREPLICAS Not enough good slaves to write.",
            "NOSCRIPT No matching script. Please use EVAL.",
            "OOM command not allowed when used memory > 'maxmemory'.",
            "READONLY You can't write against a read only slave.",
            "WRONGTYPE Operation against a key holding the wrong kind of value"
        };

        static readonly long MinimumCachedIntegerNumber = RedisConstants.NullValue; // inclusive
        const long MaximumCachedIntegerNumber = 128; // exclusive

        // cached integer size cannot larger than `int` range because of Collection.
        static readonly int SizeCachedIntegerNumber = (int)(MaximumCachedIntegerNumber - MinimumCachedIntegerNumber);

        public static readonly FixedRedisMessagePool Instance = new FixedRedisMessagePool();

        // internal caches.
        readonly Dictionary<IByteBuffer, SimpleStringRedisMessage> byteBufToSimpleStrings;
        readonly Dictionary<string, SimpleStringRedisMessage> stringToSimpleStrings;
        readonly Dictionary<IByteBuffer, ErrorRedisMessage> byteBufToErrors;
        readonly Dictionary<string, ErrorRedisMessage> stringToErrors;
        readonly Dictionary<IByteBuffer, IntegerRedisMessage> byteBufToIntegers;
        readonly Dictionary<long, IntegerRedisMessage> longToIntegers;
        readonly Dictionary<long, byte[]> longToByteBufs;

        FixedRedisMessagePool()
        {
            this.byteBufToSimpleStrings = new Dictionary<IByteBuffer, SimpleStringRedisMessage>(DefaultSimpleStrings.Length);
            this.stringToSimpleStrings = new Dictionary<string, SimpleStringRedisMessage>(DefaultSimpleStrings.Length);

            foreach (string simpleString in DefaultSimpleStrings)
            {
                IByteBuffer key = Unpooled.UnreleasableBuffer(
                    Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes(simpleString)));

                var cached = new SimpleStringRedisMessage(simpleString);
                this.byteBufToSimpleStrings.Add(key, cached);
                this.stringToSimpleStrings.Add(simpleString, cached);
            }

            this.byteBufToErrors = new Dictionary<IByteBuffer, ErrorRedisMessage>(DefaultErrors.Length);
            this.stringToErrors = new Dictionary<string, ErrorRedisMessage>(DefaultErrors.Length);
            foreach (string error in DefaultErrors)
            {
                IByteBuffer key = Unpooled.UnreleasableBuffer(
                    Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes(error)));

                var cached = new ErrorRedisMessage(error);
                this.byteBufToErrors.Add(key, cached);
                this.stringToErrors.Add(error, cached);
            }

            this.byteBufToIntegers = new Dictionary<IByteBuffer, IntegerRedisMessage>();
            this.longToIntegers = new Dictionary<long, IntegerRedisMessage>(SizeCachedIntegerNumber);
            this.longToByteBufs = new Dictionary<long, byte[]>(SizeCachedIntegerNumber);

            for (long value = MinimumCachedIntegerNumber; value < MaximumCachedIntegerNumber; value++)
            {
                byte[] keyBytes = RedisCodecUtil.LongToAsciiBytes(value);
                IByteBuffer keyByteBuf = Unpooled.UnreleasableBuffer(
                    Unpooled.WrappedBuffer(keyBytes));

                var cached = new IntegerRedisMessage(value);
                this.byteBufToIntegers.Add(keyByteBuf, cached);
                this.longToIntegers.Add(value, cached);
                this.longToByteBufs.Add(value, keyBytes);
            }
        }

        public bool TryGetSimpleString(string content, out SimpleStringRedisMessage message)
            => this.stringToSimpleStrings.TryGetValue(content, out message);

        public bool TryGetSimpleString(IByteBuffer content, out SimpleStringRedisMessage message)
            => this.byteBufToSimpleStrings.TryGetValue(content, out message);

        public bool TryGetError(string content, out ErrorRedisMessage message)
            => this.stringToErrors.TryGetValue(content, out message);

        public bool TryGetError(IByteBuffer content, out ErrorRedisMessage message)
            => this.byteBufToErrors.TryGetValue(content, out message);

        public bool TryGetInteger(long value, out IntegerRedisMessage message)
            => this.longToIntegers.TryGetValue(value, out message);

        public bool TryGetInteger(IByteBuffer content, out IntegerRedisMessage message)
            => this.byteBufToIntegers.TryGetValue(content, out message);

        public bool TryGetByteBufferOfInteger(long value, out byte[] bytes)
            => this.longToByteBufs.TryGetValue(value, out bytes);
    }
}