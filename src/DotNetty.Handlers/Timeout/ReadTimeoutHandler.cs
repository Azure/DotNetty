// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Timeout
{
    using System;
    using System.Threading;
    using DotNetty.Common.Utilities;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;
    using System.Diagnostics.Contracts;

    /// <summary>
    /// Raises a <see cref="ReadTimeoutException"/> when no data was read within a certain
    /// period of time.
    ///
    /// <pre>
    /// The connection is closed when there is no inbound traffic
    /// for 30 seconds.
    ///
    /// <example>
    /// <c>
    /// var bootstrap = new <see cref="DotNetty.Transport.Bootstrapping.ServerBootstrap"/>();
    ///
    /// bootstrap.ChildHandler(new ActionChannelInitializer&lt;ISocketChannel&gt;(channel =>
    /// {
    ///     IChannelPipeline pipeline = channel.Pipeline;
    ///     
    ///     pipeline.AddLast("readTimeoutHandler", new <see cref="ReadTimeoutHandler"/>(30);
    ///     pipeline.AddLast("myHandler", new MyHandler());
    /// } 
    /// </c>
    ///            
    /// <c>
    /// public class MyHandler : ChannelDuplexHandler 
    /// {
    ///     public override void ExceptionCaught(<see cref="IChannelHandlerContext"/> context, <see cref="Exception"/> exception)
    ///     {
    ///         if(exception is <see cref="ReadTimeoutException"/>) 
    ///         {
    ///             // do somethind
    ///         }
    ///         else
    ///         {
    ///             base.ExceptionCaught(context, cause);
    ///         }
    ///      }
    /// }
    /// </c>
    /// </example>
    /// </pre>
    /// 
    /// <seealso cref="WriteTimeoutHandler"/>
    /// <seealso cref="IdleStateHandler"/>
    /// </summary>
    public class ReadTimeoutHandler : IdleStateHandler
    {
        bool closed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetty.Handlers.Timeout.ReadTimeoutHandler"/> class.
        /// </summary>
        /// <param name="timeoutSeconds">Timeout in seconds.</param>
        public ReadTimeoutHandler(int timeoutSeconds)
            : this(TimeSpan.FromSeconds(timeoutSeconds))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetty.Handlers.Timeout.ReadTimeoutHandler"/> class.
        /// </summary>
        /// <param name="timeout">Timeout.</param>
        public ReadTimeoutHandler(TimeSpan timeout)
            : base(timeout, TimeSpan.Zero, TimeSpan.Zero)
        {
        }

        protected override void ChannelIdle(IChannelHandlerContext context, IdleStateEvent stateEvent)
        {
            Contract.Requires(stateEvent.State == IdleState.ReaderIdle);
            this.ReadTimedOut(context);
        }

        /// <summary>
        /// Is called when a read timeout was detected.
        /// </summary>
        /// <param name="context">Context.</param>
        protected virtual void ReadTimedOut(IChannelHandlerContext context)
        {
            if(!this.closed)
            {
                context.FireExceptionCaught(ReadTimeoutException.Instance);
                context.CloseAsync();
                this.closed = true;
            }
        }
    }
}

