// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Collections.Concurrent;
    using DotNetty.Common.Internal.Logging;

    /// <summary>
    ///     A special {@link ChannelHandler} which offers an easy way to initialize a {@link Channel} once it was
    ///     registered to its {@link EventLoop}.
    ///     Implementations are most often used in the context of {@link Bootstrap#handler(ChannelHandler)} ,
    ///     {@link ServerBootstrap#handler(ChannelHandler)} and {@link ServerBootstrap#childHandler(ChannelHandler)} to
    ///     setup the {@link ChannelPipeline} of a {@link Channel}.
    ///     <pre>
    ///         public class MyChannelInitializer extends {@link ChannelInitializer} {
    ///         public void initChannel({@link Channel} channel) {
    ///         channel.pipeline().addLast("myHandler", new MyHandler());
    ///         }
    ///         }
    ///         {@link ServerBootstrap} bootstrap = ...;
    ///         ...
    ///         bootstrap.childHandler(new MyChannelInitializer());
    ///         ...
    ///     </pre>
    ///     Be aware that this class is marked as {@link Sharable} and so the implementation must be safe to be re-used.
    ///     @param <T>   A sub-type of {@link Channel}
    /// </summary>
    public abstract class ChannelInitializer<T> : ChannelHandlerAdapter
        where T : IChannel
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<ChannelInitializer<T>>();

        readonly ConcurrentDictionary<IChannelHandlerContext, bool> initMap = new ConcurrentDictionary<IChannelHandlerContext, bool>(); 

        /// <summary>
        ///     This method will be called once the {@link Channel} was registered. After the method returns this instance
        ///     will be removed from the {@link ChannelPipeline} of the {@link Channel}.
        ///     @param channel            the {@link Channel} which was registered.
        ///     @throws Exception    is thrown if an error occurs. In that case the {@link Channel} will be closed.
        /// </summary>
        protected abstract void InitChannel(T channel);

        public override bool IsSharable => true;

        public sealed override void ChannelRegistered(IChannelHandlerContext ctx)
        {
            // Normally this method will never be called as handlerAdded(...) should call initChannel(...) and remove
            // the handler.
            if (this.InitChannel(ctx)) {
                // we called InitChannel(...) so we need to call now pipeline.fireChannelRegistered() to ensure we not
                // miss an event.
                ctx.Channel.Pipeline.FireChannelRegistered();
            } else {
                // Called InitChannel(...) before which is the expected behavior, so just forward the event.
                ctx.FireChannelRegistered();
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            Logger.Warn("Failed to initialize a channel. Closing: " + ctx.Channel, cause);
            ctx.CloseAsync();
        }

        public override void HandlerAdded(IChannelHandlerContext ctx)
        {
            if (ctx.Channel.Registered)
            {
                // This should always be true with our current DefaultChannelPipeline implementation.
                // The good thing about calling InitChannel(...) in HandlerAdded(...) is that there will be no ordering
                // surprises if a ChannelInitializer will add another ChannelInitializer. This is as all handlers
                // will be added in the expected order.
                this.InitChannel(ctx);
            }
        }

        bool InitChannel(IChannelHandlerContext ctx)
        {
            if (initMap.TryAdd(ctx, true)) // Guard against re-entrance.
            {
                try
                {
                    this.InitChannel((T) ctx.Channel);
                }
                catch (Exception cause)
                {
                    // Explicitly call exceptionCaught(...) as we removed the handler before calling initChannel(...).
                    // We do so to prevent multiple calls to initChannel(...).
                    this.ExceptionCaught(ctx, cause);
                }
                finally
                {
                    this.Remove(ctx);
                }
                return true;
            }
            return false;
        }

        void Remove(IChannelHandlerContext ctx)
        {
            try
            {
                IChannelPipeline pipeline = ctx.Channel.Pipeline;
                if (pipeline.Context(this) != null)
                {
                    pipeline.Remove(this);
                }
            }
            finally
            {
                initMap.TryRemove(ctx, out bool removed);
            }
        }
    }
}