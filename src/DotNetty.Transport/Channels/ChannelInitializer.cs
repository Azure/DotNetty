// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using DotNetty.Common.Internal.Logging;

    /// <summary>
    /// A special {@link ChannelHandler} which offers an easy way to initialize a {@link Channel} once it was
    /// registered to its {@link EventLoop}.
    ///
    /// Implementations are most often used in the context of {@link Bootstrap#handler(ChannelHandler)} ,
    /// {@link ServerBootstrap#handler(ChannelHandler)} and {@link ServerBootstrap#childHandler(ChannelHandler)} to
    /// setup the {@link ChannelPipeline} of a {@link Channel}.
    ///
    /// <pre>
    ///
    /// public class MyChannelInitializer extends {@link ChannelInitializer} {
    ///     public void initChannel({@link Channel} channel) {
    ///         channel.pipeline().addLast("myHandler", new MyHandler());
    ///     }
    /// }
    ///
    /// {@link ServerBootstrap} bootstrap = ...;
    /// ...
    /// bootstrap.childHandler(new MyChannelInitializer());
    /// ...
    /// </pre>
    /// Be aware that this class is marked as {@link Sharable} and so the implementation must be safe to be re-used.
    ///
    /// @param <T>   A sub-type of {@link Channel}
    /// </summary>
    public abstract class ChannelInitializer<T> : ChannelHandlerAdapter
        where T : IChannel
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<ChannelInitializer<T>>();

        /// <summary>
        /// This method will be called once the {@link Channel} was registered. After the method returns this instance
        /// will be removed from the {@link ChannelPipeline} of the {@link Channel}.
        ///
        /// @param channel            the {@link Channel} which was registered.
        /// @throws Exception    is thrown if an error occurs. In that case the {@link Channel} will be closed.
        /// </summary>
        protected abstract void InitChannel(T channel);

        public override bool IsSharable
        {
            get { return true; }
        }

        public sealed override void ChannelRegistered(IChannelHandlerContext context)
        {
            IChannelPipeline pipeline = context.Channel.Pipeline;
            bool success = false;
            try
            {
                this.InitChannel((T)context.Channel);
                pipeline.Remove(this);
                context.FireChannelRegistered();
                success = true;
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to initialize a channel. Closing: " + context.Channel, ex);
            }
            finally
            {
                if (pipeline.Context(this) != null)
                {
                    pipeline.Remove(this);
                }
                if (!success)
                {
                    context.CloseAsync();
                }
            }
        }
    }
}