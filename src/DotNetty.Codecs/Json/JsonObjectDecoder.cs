// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Json
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    /// <summary>
    ///     Splits a byte stream of JSON objects and arrays into individual objects/arrays and passes them up the
    ///     <see cref="DotNetty.Transport.Channels.IChannelPipeline" />.
    ///     This class does not do any real parsing or validation. A sequence of bytes is considered a JSON object/array
    ///     if it contains a matching number of opening and closing braces/brackets. It's up to a subsequent
    ///     <see cref="DotNetty.Transport.Channels.IChannelHandler" />
    ///     to parse the JSON text into a more usable form i.e.a POCO.
    /// </summary>
    public class JsonObjectDecoder : ByteToMessageDecoder
    {
        const int StCorrupted = -1;
        const int StInit = 0;
        const int StDecodingNormal = 1;
        const int StDecodingArrayStream = 2;

        int openBraces;
        int idx;

        int state;
        bool insideString;

        readonly int maxObjectLength;
        readonly bool streamArrayElements;

        public JsonObjectDecoder()
            : this(1024 * 1024)
        {
        }

        public JsonObjectDecoder(int maxObjectLength)
            : this(maxObjectLength, false)
        {
        }

        public JsonObjectDecoder(bool streamArrayElements)
            : this(1024 * 1024, streamArrayElements)
        {
        }

        public JsonObjectDecoder(int maxObjectLength, bool streamArrayElements)
        {
            if (maxObjectLength < 1)
            {
                throw new ArgumentException("maxObjectLength must be a positive int");
            }
            this.maxObjectLength = maxObjectLength;
            this.streamArrayElements = streamArrayElements;
        }

        protected internal override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            if (this.state == StCorrupted)
            {
                input.SkipBytes(input.ReadableBytes);
                return;
            }

            // index of next byte to process.
            int idx = this.idx;
            int wrtIdx = input.WriterIndex;

            if (wrtIdx > this.maxObjectLength)
            {
                // buffer size exceeded maxObjectLength; discarding the complete buffer.
                input.SkipBytes(input.ReadableBytes);
                this.Reset();
                throw new TooLongFrameException($"Object length exceeds {this.maxObjectLength}: {wrtIdx} bytes discarded");
            }

            for ( /* use current idx */; idx < wrtIdx; idx++)
            {
                byte c = input.GetByte(idx);

                if (this.state == StDecodingNormal)
                {
                    this.DecodeByte(c, input, idx);

                    // All opening braces/brackets have been closed. That's enough to conclude
                    // that the JSON object/array is complete.
                    if (this.openBraces == 0)
                    {
                        IByteBuffer json = this.ExtractObject(context, input, input.ReaderIndex, idx + 1 - input.ReaderIndex);
                        if (json != null)
                        {
                            output.Add(json);
                        }

                        // The JSON object/array was extracted => discard the bytes from
                        // the input buffer.
                        input.SetReaderIndex(idx + 1);

                        // Reset the object state to get ready for the next JSON object/text
                        // coming along the byte stream.
                        this.Reset();
                    }
                }
                else if (this.state == StDecodingArrayStream)
                {
                    this.DecodeByte(c, input, idx);

                    if (!this.insideString && (this.openBraces == 1 && c == ',' || this.openBraces == 0 && c == ']'))
                    {
                        // skip leading spaces. No range check is needed and the loop will terminate
                        // because the byte at position idx is not a whitespace.
                        for (int i = input.ReaderIndex; char.IsWhiteSpace(Convert.ToChar(input.GetByte(i))); i++)
                        {
                            input.SkipBytes(1);
                        }

                        // skip trailing spaces.
                        int idxNoSpaces = idx - 1;
                        while (idxNoSpaces >= input.ReaderIndex && char.IsWhiteSpace(Convert.ToChar(input.GetByte(idxNoSpaces))))
                        {
                            idxNoSpaces--;
                        }

                        IByteBuffer json = this.ExtractObject(context, input, input.ReaderIndex, idxNoSpaces + 1 - input.ReaderIndex);
                        if (json != null)
                        {
                            output.Add(json);
                        }

                        input.SetReaderIndex(idx + 1);

                        if (c == ']')
                        {
                            this.Reset();
                        }
                    }
                }
                else if (c == '{' || c == '[') // JSON object/array detected. Accumulate bytes until all braces/brackets are closed
                {
                    this.InitDecoding(c);

                    if (this.state == StDecodingArrayStream)
                    {
                        //Discard the array bracket
                        input.SkipBytes(1);
                    }
                }
                else if (char.IsWhiteSpace(Convert.ToChar(c))) //Discard leading spaces in from of a JSON object/array
                {
                    input.SkipBytes(1);
                }
                else
                {
                    this.state = StCorrupted;
                    throw new CorruptedFrameException($"Invalid JSON received at byte position {idx}");
                }
            }
            if (input.ReadableBytes == 0)
            {
                this.idx = 0;
            }
            else
            {
                this.idx = idx;
            }
        }

        protected virtual IByteBuffer ExtractObject(IChannelHandlerContext context, IByteBuffer buffer, int index, int length)
        {
            IByteBuffer buff = buffer.Slice(index, length);
            buff.Retain();
            return buff;
        }

        void DecodeByte(byte c, IByteBuffer input, int idx)
        {
            if ((c == '{' || c == '[') && !this.insideString)
            {
                this.openBraces++;
            }
            else if ((c == '}' || c == ']') && !this.insideString)
            {
                this.openBraces--;
            }
            else if (c == '"')
            {
                // start of a new JSON string. It's necessary to detect strings as they may
                // also contain braces/brackets and that could lead to incorrect results.
                if (!this.insideString)
                {
                    this.insideString = true;
                    // If the double quote wasn't escaped then this is the end of a string.
                }
                else if (input.GetByte(idx - 1) != '\\')
                {
                    this.insideString = false;
                }
            }
        }

        void InitDecoding(byte openingBrace)
        {
            this.openBraces = 1;
            if (openingBrace == '[' && this.streamArrayElements)
            {
                this.state = StDecodingArrayStream;
            }
            else
            {
                this.state = StDecodingNormal;
            }
        }

        void Reset()
        {
            this.insideString = false;
            this.state = StInit;
            this.openBraces = 0;
        }
    }
}