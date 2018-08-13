// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Transport.Channels;

    public abstract class ByteToMessageDecoder : ChannelHandlerAdapter
    {
        public delegate IByteBuffer CumulationFunc(IByteBufferAllocator alloc, IByteBuffer cumulation, IByteBuffer input);

        /// <summary>
        ///     Cumulates instances of <see cref="IByteBuffer" /> by merging them into one <see cref="IByteBuffer" />, using memory
        ///     copies.
        /// </summary>
        public static readonly CumulationFunc MergeCumulator = (allocator, cumulation, input) =>
        {
            IByteBuffer buffer;
            if (cumulation.WriterIndex > cumulation.MaxCapacity - input.ReadableBytes
                || cumulation.ReferenceCount > 1)
            {
                // Expand cumulation (by replace it) when either there is not more room in the buffer
                // or if the refCnt is greater then 1 which may happen when the user use Slice().Retain() or
                // Duplicate().Retain().
                //
                // See:
                // - https://github.com/netty/netty/issues/2327
                // - https://github.com/netty/netty/issues/1764
                buffer = ExpandCumulation(allocator, cumulation, input.ReadableBytes);
            }
            else
            {
                buffer = cumulation;
            }
            buffer.WriteBytes(input);
            input.Release();
            return buffer;
        };

        /// <summary>
        ///     Cumulate instances of <see cref="IByteBuffer" /> by add them to a <see cref="CompositeByteBuffer" /> and therefore
        ///     avoiding memory copy when possible.
        /// </summary>
        /// <remarks>
        ///     Be aware that <see cref="CompositeByteBuffer" /> use a more complex indexing implementation so depending on your
        ///     use-case
        ///     and the decoder implementation this may be slower then just use the <see cref="MergeCumulator" />.
        /// </remarks>
        public static CumulationFunc CompositionCumulation = (alloc, cumulation, input) =>
        {
            IByteBuffer buffer;
            if (cumulation.ReferenceCount > 1)
            {
                // Expand cumulation (by replace it) when the refCnt is greater then 1 which may happen when the user
                // use slice().retain() or duplicate().retain().
                //
                // See:
                // - https://github.com/netty/netty/issues/2327
                // - https://github.com/netty/netty/issues/1764
                buffer = ExpandCumulation(alloc, cumulation, input.ReadableBytes);
                buffer.WriteBytes(input);
                input.Release();
            }
            else
            {
                CompositeByteBuffer composite;
                var asComposite = cumulation as CompositeByteBuffer;
                if (asComposite != null)
                {
                    composite = asComposite;
                }
                else
                {
                    int readable = cumulation.ReadableBytes;
                    composite = alloc.CompositeBuffer();
                    composite.AddComponent(cumulation).SetWriterIndex(readable);
                }
                composite.AddComponent(input).SetWriterIndex(composite.WriterIndex + input.ReadableBytes);
                buffer = composite;
            }
            return buffer;
        };

        IByteBuffer cumulation;
        CumulationFunc cumulator = MergeCumulator;
        bool decodeWasNull;
        bool first;

        protected ByteToMessageDecoder()
        {
            // ReSharper disable once DoNotCallOverridableMethodsInConstructor -- used for safety check only
            if (this.IsSharable)
            {
                throw new InvalidOperationException($"Decoders inheriting from {typeof(ByteToMessageDecoder).Name} cannot be sharable.");
            }
        }

        /// <summary>
        ///     Determines whether only one message should be decoded per <see cref="ChannelRead" /> call.
        ///     Default is <code>false</code> as this has performance impacts.
        /// </summary>
        /// <remarks>Is particularly useful in support of protocol upgrade scenarios.</remarks>
        public bool SingleDecode { get; set; }

        public void SetCumulator(CumulationFunc cumulationFunc)
        {
            Contract.Requires(cumulationFunc != null);

            this.cumulator = cumulationFunc;
        }

        /// <summary>
        ///     Returns the actual number of readable bytes in the internal cumulative
        ///     buffer of this decoder. You usually do not need to rely on this value
        ///     to write a decoder. Use it only when you must use it at your own risk.
        ///     This method is a shortcut to <see cref="IByteBuffer.ReadableBytes" /> of <see cref="InternalBuffer" />.
        /// </summary>
        protected int ActualReadableBytes => this.InternalBuffer.ReadableBytes;

        protected IByteBuffer InternalBuffer
        {
            get
            {
                if (this.cumulation != null)
                {
                    return this.cumulation;
                }
                else
                {
                    return Unpooled.Empty;
                }
            }
        }

        protected internal abstract void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output);

        static IByteBuffer ExpandCumulation(IByteBufferAllocator allocator, IByteBuffer cumulation, int readable)
        {
            IByteBuffer oldCumulation = cumulation;
            cumulation = allocator.Buffer(oldCumulation.ReadableBytes + readable);
            cumulation.WriteBytes(oldCumulation);
            oldCumulation.Release();
            return cumulation;
        }

        public override void HandlerRemoved(IChannelHandlerContext context)
        {
            IByteBuffer buf = this.InternalBuffer;

            // Directly set this to null so we are sure we not access it in any other method here anymore.
            this.cumulation = null;
            int readable = buf.ReadableBytes;
            if (readable > 0)
            {
                IByteBuffer bytes = buf.ReadBytes(readable);
                buf.Release();
                context.FireChannelRead(bytes);
            }
            else
            {
                buf.Release();
            }

            context.FireChannelReadComplete();
            this.HandlerRemovedInternal(context);
        }

        protected virtual void HandlerRemovedInternal(IChannelHandlerContext context)
        {
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            var data = message as IByteBuffer;
            if (data != null)
            {
                ThreadLocalObjectList output = ThreadLocalObjectList.NewInstance();
                try
                {
                    this.first = this.cumulation == null;
                    if (this.first)
                    {
                        this.cumulation = data;
                    }
                    else
                    {
                        this.cumulation = this.cumulator(context.Allocator, this.cumulation, data);
                    }
                    this.CallDecode(context, this.cumulation, output);
                }
                catch (DecoderException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new DecoderException(ex);
                }
                finally
                {
                    if (this.cumulation != null && !this.cumulation.IsReadable())
                    {
                        this.cumulation.Release();
                        this.cumulation = null;
                    }
                    int size = output.Count;
                    this.decodeWasNull = size == 0;

                    for (int i = 0; i < size; i++)
                    {
                        context.FireChannelRead(output[i]);
                    }
                    output.Return();
                }
            }
            else
            {
                context.FireChannelRead(message);
            }
        }

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            this.DiscardSomeReadBytes();
            if (this.decodeWasNull)
            {
                this.decodeWasNull = false;
                if (!context.Channel.Configuration.AutoRead)
                {
                    context.Read();
                }
            }
            context.FireChannelReadComplete();
        }

        protected void DiscardSomeReadBytes()
        {
            if (this.cumulation != null && !this.first && this.cumulation.ReferenceCount == 1)
            {
                // discard some bytes if possible to make more room input the
                // buffer but only if the refCnt == 1  as otherwise the user may have
                // used slice().retain() or duplicate().retain().
                //
                // See:
                // - https://github.com/netty/netty/issues/2327
                // - https://github.com/netty/netty/issues/1764
                this.cumulation.DiscardSomeReadBytes();
            }
        }

        public override void ChannelInactive(IChannelHandlerContext ctx)
        {
            ThreadLocalObjectList output = ThreadLocalObjectList.NewInstance();
            try
            {
                if (this.cumulation != null)
                {
                    this.CallDecode(ctx, this.cumulation, output);
                    this.DecodeLast(ctx, this.cumulation, output);
                }
                else
                {
                    this.DecodeLast(ctx, Unpooled.Empty, output);
                }
            }
            catch (DecoderException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                throw new DecoderException(e);
            }
            finally
            {
                try
                {
                    if (this.cumulation != null)
                    {
                        this.cumulation.Release();
                        this.cumulation = null;
                    }
                    int size = output.Count;
                    for (int i = 0; i < size; i++)
                    {
                        ctx.FireChannelRead(output[i]);
                    }
                    if (size > 0)
                    {
                        // Something was read, call fireChannelReadComplete()
                        ctx.FireChannelReadComplete();
                    }
                    ctx.FireChannelInactive();
                }
                finally
                {
                    // recycle in all cases
                    output.Return();
                }
            }
        }

        protected virtual void CallDecode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            Contract.Requires(context != null);
            Contract.Requires(input != null);
            Contract.Requires(output != null);

            try
            {
                while (input.IsReadable())
                {
                    int initialOutputCount = output.Count;
                    int oldInputLength = input.ReadableBytes;
                    this.Decode(context, input, output);

                    // Check if this handler was removed before continuing the loop.
                    // If it was removed, it is not safe to continue to operate on the buffer.
                    //
                    // See https://github.com/netty/netty/issues/1664
                    if (context.Removed)
                    {
                        break;
                    }

                    if (initialOutputCount == output.Count)
                    {
                        // no outgoing messages have been produced

                        if (oldInputLength == input.ReadableBytes)
                        {
                            break;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    if (oldInputLength == input.ReadableBytes)
                    {
                        throw new DecoderException($"{this.GetType().Name}.Decode() did not read anything but decoded a message.");
                    }

                    if (this.SingleDecode)
                    {
                        break;
                    }
                }
            }
            catch (DecoderException)
            {
                throw;
            }
            catch (Exception cause)
            {
                throw new DecoderException(cause);
            }
        }

        protected virtual void DecodeLast(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            if (input.IsReadable())
            {
                // Only call decode() if there is something left in the buffer to decode.
                // See https://github.com/netty/netty/issues/4386
                this.Decode(context, input, output);
            }
        }
    }
}