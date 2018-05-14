// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Compression
{
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    public class JZlibDecoder : ZlibDecoder
    {
        readonly Inflater z = new Inflater();
        readonly byte[] dictionary;
        volatile bool finished;

        public JZlibDecoder() : this(ZlibWrapper.ZlibOrNone)
        {
        }

        public JZlibDecoder(ZlibWrapper wrapper)
        {
            int resultCode = this.z.Init(ZlibUtil.ConvertWrapperType(wrapper));
            if (resultCode != JZlib.Z_OK)
            {
                ZlibUtil.Fail(this.z, "initialization failure", resultCode);
            }
        }

        public JZlibDecoder(byte[] dictionary)
        {
            Contract.Requires(dictionary != null);
            this.dictionary = dictionary;

            int resultCode;
            resultCode = this.z.InflateInit(JZlib.W_ZLIB);
            if (resultCode != JZlib.Z_OK)
            {
                ZlibUtil.Fail(this.z, "initialization failure", resultCode);
            }
        }

        public override bool IsClosed => this.finished;

        protected internal override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            if (this.finished)
            {
                // Skip data received after finished.
                input.SkipBytes(input.ReadableBytes);
                return;
            }

            int inputLength = input.ReadableBytes;
            if (inputLength == 0)
            {
                return;
            }

            try
            {
                // Configure input.
                this.z.avail_in = inputLength;
                if (input.HasArray)
                {
                    this.z.next_in = input.Array;
                    this.z.next_in_index = input.ArrayOffset + input.ReaderIndex;
                }
                else
                {
                    var array = new byte[inputLength];
                    input.GetBytes(input.ReaderIndex, array);
                    this.z.next_in = array;
                    this.z.next_in_index = 0;
                }
                int oldNextInIndex = this.z.next_in_index;

                // Configure output.
                int maxOutputLength = inputLength << 1;
                IByteBuffer decompressed = context.Allocator.Buffer(maxOutputLength);

                try
                {
                    while (true)
                    {
                        this.z.avail_out = maxOutputLength;
                        decompressed.EnsureWritable(maxOutputLength);
                        this.z.next_out = decompressed.Array;
                        this.z.next_out_index = decompressed.ArrayOffset + decompressed.WriterIndex;
                        int oldNextOutIndex = this.z.next_out_index;

                        // Decompress 'in' into 'out'
                        int resultCode = this.z.Inflate(JZlib.Z_SYNC_FLUSH);
                        int outputLength = this.z.next_out_index - oldNextOutIndex;
                        if (outputLength > 0)
                        {
                            decompressed.SetWriterIndex(decompressed.WriterIndex + outputLength);
                        }

                        if (resultCode == JZlib.Z_NEED_DICT)
                        {
                            if (this.dictionary == null)
                            {
                                ZlibUtil.Fail(this.z, "decompression failure", resultCode);
                            }
                            else
                            {
                                resultCode = this.z.InflateSetDictionary(this.dictionary, this.dictionary.Length);
                                if (resultCode != JZlib.Z_OK)
                                {
                                    ZlibUtil.Fail(this.z, "failed to set the dictionary", resultCode);
                                }
                            }
                            continue;
                        }
                        if (resultCode == JZlib.Z_STREAM_END)
                        {
                            this.finished = true; // Do not decode anymore.
                            this.z.InflateEnd();
                            break;
                        }
                        if (resultCode == JZlib.Z_OK)
                        {
                           continue;
                        }
                        if (resultCode == JZlib.Z_BUF_ERROR)
                        {
                            if (this.z.avail_in <= 0)
                            {
                                break;
                            }

                            continue;
                        }
                        //default
                        ZlibUtil.Fail(this.z, "decompression failure", resultCode);
                    }
                }
                finally
                {
                     input.SkipBytes(this.z.next_in_index - oldNextInIndex);
                    if (decompressed.IsReadable())
                    {
                        output.Add(decompressed);
                    }
                    else
                    {
                        decompressed.Release();
                    }
                }
            }
            finally
            {
                this.z.next_in = null;
                this.z.next_out = null;
            }

        }
    }
}
