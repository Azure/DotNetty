// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;

    /// <summary>
    /// A list of {@link ChannelHandler}s which handles or intercepts inbound events and outbound operations of a
    /// {@link Channel}.  {@link ChannelPipeline} implements an advanced form of the
    /// <a href="http://www.oracle.com/technetwork/java/interceptingfilter-142169.html">Intercepting Filter</a> pattern
    /// to give a user full control over how an event is handled and how the {@link ChannelHandler}s in a pipeline
    /// interact with each other.
    ///
    /// <h3>Creation of a pipeline</h3>
    ///
    /// Each channel has its own pipeline and it is created automatically when a new channel is created.
    ///
    /// <h3>How an event flows in a pipeline</h3>
    ///
    /// The following diagram describes how I/O events are processed by {@link ChannelHandler}s in a {@link ChannelPipeline}
    /// typically. An I/O event is handled by a {@link ChannelHandler} and is forwarded by the {@link ChannelHandler} which
    /// handled the event to the {@link ChannelHandler} which is placed right next to it. A {@link ChannelHandler} can also
    /// trigger an arbitrary I/O event if necessary.  To forward or trigger an event, a {@link ChannelHandler} calls the
    /// event propagation methods defined in {@link ChannelHandlerContext}, such as
    /// {@link ChannelHandlerContext#fireChannelRead(Object)} and {@link ChannelHandlerContext#write(Object)}.
    /// <pre>
    ///                                                 I/O Request
    ///                                            via {@link Channel} or
    ///                                        {@link ChannelHandlerContext}
    ///                                                      |
    ///  +---------------------------------------------------+---------------+
    ///  |                           ChannelPipeline         |               |
    ///  |                                                  \|/              |
    ///  |    +----------------------------------------------+----------+    |
    ///  |    |                   ChannelHandler  N                     |    |
    ///  |    +----------+-----------------------------------+----------+    |
    ///  |              /|\                                  |               |
    ///  |               |                                  \|/              |
    ///  |    +----------+-----------------------------------+----------+    |
    ///  |    |                   ChannelHandler N-1                    |    |
    ///  |    +----------+-----------------------------------+----------+    |
    ///  |              /|\                                  .               |
    ///  |               .                                   .               |
    ///  | ChannelHandlerContext.fireIN_EVT() ChannelHandlerContext.OUT_EVT()|
    ///  |          [method call]                      [method call]         |
    ///  |               .                                   .               |
    ///  |               .                                  \|/              |
    ///  |    +----------+-----------------------------------+----------+    |
    ///  |    |                   ChannelHandler  2                     |    |
    ///  |    +----------+-----------------------------------+----------+    |
    ///  |              /|\                                  |               |
    ///  |               |                                  \|/              |
    ///  |    +----------+-----------------------------------+----------+    |
    ///  |    |                   ChannelHandler  1                     |    |
    ///  |    +----------+-----------------------------------+----------+    |
    ///  |              /|\                                  |               |
    ///  +---------------+-----------------------------------+---------------+
    ///                  |                                  \|/
    ///  +---------------+-----------------------------------+---------------+
    ///  |               |                                   |               |
    ///  |       [ Socket.read() ]                    [ Socket.write() ]     |
    ///  |                                                                   |
    ///  |  Netty Internal I/O Threads (Transport Implementation)            |
    ///  +-------------------------------------------------------------------+
    /// </pre>
    /// An inbound event is handled by the {@link ChannelHandler}s in the bottom-up direction as shown on the left side of
    /// the diagram.  An inbound event is usually triggered by the I/O thread on the bottom of the diagram so that the
    /// {@link ChannelHandler}s are notified when the state of a {@link Channel} changes (e.g. newly established connections
    /// and closed connections) or the inbound data was read from a remote peer.  If an inbound event goes beyond the
    /// {@link ChannelHandler} at the top of the diagram, it is discarded and logged, depending on your loglevel.
    ///
    /// <p>
    /// An outbound event is handled by the {@link ChannelHandler}s in the top-down direction as shown on the right side of
    /// the diagram.  An outbound event is usually triggered by your code that requests an outbound I/O operation, such as
    /// a write request and a connection attempt.  If an outbound event goes beyond the {@link ChannelHandler} at the
    /// bottom of the diagram, it is handled by an I/O thread associated with the {@link Channel}. The I/O thread often
    /// performs the actual output operation such as {@link SocketChannel#write(ByteBuffer)}.
    /// <p>
    ///
    /// <h3>Forwarding an event to the next handler</h3>
    ///
    /// As explained briefly above, a {@link ChannelHandler} has to invoke the event propagation methods in
    /// {@link ChannelHandlerContext} to forward an event to its next handler.  Those methods include:
    /// <ul>
    /// <li>Inbound event propagation methods:
    ///     <ul>
    ///     <li>{@link ChannelHandlerContext#fireChannelRegistered()}</li>
    ///     <li>{@link ChannelHandlerContext#fireChannelActive()}</li>
    ///     <li>{@link ChannelHandlerContext#fireChannelRead(Object)}</li>
    ///     <li>{@link ChannelHandlerContext#fireChannelReadComplete()}</li>
    ///     <li>{@link ChannelHandlerContext#fireExceptionCaught(Throwable)}</li>
    ///     <li>{@link ChannelHandlerContext#fireUserEventTriggered(Object)}</li>
    ///     <li>{@link ChannelHandlerContext#fireChannelWritabilityChanged()}</li>
    ///     <li>{@link ChannelHandlerContext#fireChannelInactive()}</li>
    ///     </ul>
    /// </li>
    /// <li>Outbound event propagation methods:
    ///     <ul>
    ///     <li>{@link ChannelHandlerContext#bind(SocketAddress, ChannelPromise)}</li>
    ///     <li>{@link ChannelHandlerContext#connect(SocketAddress, SocketAddress, ChannelPromise)}</li>
    ///     <li>{@link ChannelHandlerContext#write(Object, ChannelPromise)}</li>
    ///     <li>{@link ChannelHandlerContext#flush()}</li>
    ///     <li>{@link ChannelHandlerContext#read()}</li>
    ///     <li>{@link ChannelHandlerContext#disconnect(ChannelPromise)}</li>
    ///     <li>{@link ChannelHandlerContext#close(ChannelPromise)}</li>
    ///     </ul>
    /// </li>
    /// </ul>
    ///
    /// and the following example shows how the event propagation is usually done:
    ///
    /// <pre>
    /// public class MyInboundHandler extends {@link ChannelHandlerAdapter} {
    ///     {@code }
    ///     public void channelActive({@link ChannelHandlerContext} ctx) {
    ///         System.out.println("Connected!");
    ///         ctx.fireChannelActive();
    ///     }
    /// }
    ///
    /// public clas MyOutboundHandler extends {@link ChannelHandlerAdapter} {
    ///     {@code }
    ///     public void close({@link ChannelHandlerContext} ctx, {@link ChannelPromise} promise) {
    ///         System.out.println("Closing ..");
    ///         ctx.close(promise);
    ///     }
    /// }
    /// </pre>
    ///
    /// <h3>Building a pipeline</h3>
    /// <p>
    /// A user is supposed to have one or more {@link ChannelHandler}s in a pipeline to receive I/O events (e.g. read) and
    /// to request I/O operations (e.g. write and close).  For example, a typical server will have the following handlers
    /// in each channel's pipeline, but your mileage may vary depending on the complexity and characteristics of the
    /// protocol and business logic:
    ///
    /// <ol>
    /// <li>Protocol Decoder - translates binary data (e.g. {@link ByteBuf}) into a Java object.</li>
    /// <li>Protocol Encoder - translates a Java object into binary data.</li>
    /// <li>Business Logic Handler - performs the actual business logic (e.g. database access).</li>
    /// </ol>
    ///
    /// and it could be represented as shown in the following example:
    ///
    /// <pre>
    /// static final {@link EventExecutorGroup} group = new {@link DefaultEventExecutorGroup}(16);
    /// ...
    ///
    /// {@link ChannelPipeline} pipeline = ch.pipeline();
    ///
    /// pipeline.addLast("decoder", new MyProtocolDecoder());
    /// pipeline.addLast("encoder", new MyProtocolEncoder());
    ///
    /// // Tell the pipeline to run MyBusinessLogicHandler's event handler methods
    /// // in a different thread than an I/O thread so that the I/O thread is not blocked by
    /// // a time-consuming task.
    /// // If your business logic is fully asynchronous or finished very quickly, you don't
    /// // need to specify a group.
    /// pipeline.addLast(group, "handler", new MyBusinessLogicHandler());
    /// </pre>
    ///
    /// <h3>Thread safety</h3>
    /// <p>
    /// A {@link ChannelHandler} can be added or removed at any time because a {@link ChannelPipeline} is thread safe.
    /// For example, you can insert an encryption handler when sensitive information is about to be exchanged, and remove it
    /// after the exchange.
    /// </summary>
    public interface IChannelPipeline : IEnumerable<IChannelHandler>
    {
        /// <summary>
        /// Inserts a {@link ChannelHandler} at the first position of this pipeline.
        /// </summary>
        ///
        /// @param name     the name of the handler to insert first. {@code null} to let the name auto-generated.
        /// @param handler  the handler to insert first
        ///
        /// @throws IllegalArgumentException
        ///         if there's an entry with the same name already in the pipeline
        /// @throws NullPointerException
        ///         if the specified handler is {@code null}
        IChannelPipeline AddFirst(string name, IChannelHandler handler);

        /// <summary>
        /// Inserts a {@link ChannelHandler} at the first position of this pipeline.
        /// </summary>
        /// <param name="invoker">the {@link ChannelHandlerInvoker} which invokes the {@code handler}s event handler methods</param>
        /// <param name="name">the name of the handler to insert first. <code>null</code> to let the name auto-generated.</param>
        /// <param name="handler">the handler to insert first</param>
        /// <exception cref="ArgumentException">if there's an entry with the same name already in the pipeline</exception>
        /// <exception cref="ArgumentNullException">if the specified handler is <code>null</code></exception>
        IChannelPipeline AddFirst(IChannelHandlerInvoker invoker, string name, IChannelHandler handler);

        /// <summary>
        /// Appends a {@link ChannelHandler} at the last position of this pipeline.
        ///
        /// @param name     the name of the handler to append. {@code null} to let the name auto-generated.
        /// @param handler  the handler to append
        ///
        /// @throws IllegalArgumentException
        ///         if there's an entry with the same name already in the pipeline
        /// @throws NullPointerException
        ///         if the specified handler is {@code null}
        /// </summary>
        IChannelPipeline AddLast(string name, IChannelHandler handler);

        /// <summary>
        ///     Appends a {@link ChannelHandler} at the last position of this pipeline.
        /// </summary>
        /// <param name="invoker">the {@link ChannelHandlerInvoker} which invokes the {@code handler}s event handler methods</param>
        /// <param name="name">the name of the handler to append. {@code null} to let the name auto-generated.</param>
        /// <param name="handler">the handler to append</param>
        /// <exception cref="ArgumentException">if there's an entry with the same name already in the pipeline</exception>
        /// <exception cref="ArgumentNullException">if the specified handler is <code>null</code></exception>
        IChannelPipeline AddLast(IChannelHandlerInvoker invoker, string name, IChannelHandler handler);

        /// <summary>
        ///     Inserts a {@link ChannelHandler} before an existing handler of this pipeline.
        /// </summary>
        /// <param name="baseName">the name of the existing handler</param>
        /// <param name="name">the name of the handler to insert before. {@code null} to let the name auto-generated.</param>
        /// <param name="handler">the handler to insert before</param>
        /// @throws NoSuchElementException
        /// if there's no such entry with the specified {@code baseName}
        /// @throws IllegalArgumentException
        /// if there's an entry with the same name already in the pipeline
        /// @throws NullPointerException
        /// if the specified baseName or handler is {@code null}
        IChannelPipeline AddBefore(string baseName, string name, IChannelHandler handler);

        /// <summary>
        ///     Inserts a {@link ChannelHandler} before an existing handler of this pipeline.
        /// </summary>
        /// <param name="invoker">the {@link ChannelHandlerInvoker} which invokes the {@code handler}s event handler methods</param>
        /// <param name="baseName">the name of the existing handler</param>
        /// <param name="name">the name of the handler to insert before. {@code null} to let the name auto-generated.</param>
        /// <param name="handler">the handler to insert before</param>
        /// @throws NoSuchElementException
        /// if there's no such entry with the specified {@code baseName}
        /// @throws IllegalArgumentException
        /// if there's an entry with the same name already in the pipeline
        /// @throws NullPointerException
        /// if the specified baseName or handler is {@code null}
        IChannelPipeline AddBefore(IChannelHandlerInvoker invoker, string baseName, string name, IChannelHandler handler);

        /// <summary>
        ///     Inserts a {@link ChannelHandler} after an existing handler of this pipeline.
        /// </summary>
        /// <param name="baseName">the name of the existing handler</param>
        /// <param name="name">the name of the handler to insert after. {@code null} to let the name auto-generated.</param>
        /// <param name="handler">the handler to insert after</param>
        /// @throws NoSuchElementException
        /// if there's no such entry with the specified {@code baseName}
        /// @throws IllegalArgumentException
        /// if there's an entry with the same name already in the pipeline
        /// @throws NullPointerException
        /// if the specified baseName or handler is {@code null}
        IChannelPipeline AddAfter(string baseName, string name, IChannelHandler handler);

        /// <summary>
        ///     Inserts a {@link ChannelHandler} after an existing handler of this pipeline.
        /// </summary>
        /// <param name="invoker">the {@link ChannelHandlerInvoker} which invokes the {@code handler}s event handler methods</param>
        /// <param name="baseName">the name of the existing handler</param>
        /// <param name="name">the name of the handler to insert after. {@code null} to let the name auto-generated.</param>
        /// <param name="handler">the handler to insert after</param>
        /// @throws NoSuchElementException
        /// if there's no such entry with the specified {@code baseName}
        /// @throws IllegalArgumentException
        /// if there's an entry with the same name already in the pipeline
        /// @throws NullPointerException
        /// if the specified baseName or handler is {@code null}
        IChannelPipeline AddAfter(IChannelHandlerInvoker invoker, string baseName, string name, IChannelHandler handler);

        /// <summary>
        /// Inserts a {@link ChannelHandler}s at the first position of this pipeline.
        ///
        /// @param handlers  the handlers to insert first
        ///
        /// </summary>
        IChannelPipeline AddFirst(params IChannelHandler[] handlers);

        /// <summary>
        /// Inserts a {@link ChannelHandler}s at the first position of this pipeline.
        ///
        /// @param invoker   the {@link ChannelHandlerInvoker} which invokes the {@code handler}s event handler methods
        /// @param handlers  the handlers to insert first
        ///
        /// </summary>
        IChannelPipeline AddFirst(IChannelHandlerInvoker invoker, params IChannelHandler[] handlers);

        /// <summary>
        /// Inserts a {@link ChannelHandler}s at the last position of this pipeline.
        ///
        /// @param handlers  the handlers to insert last
        ///
        /// </summary>
        IChannelPipeline AddLast(params IChannelHandler[] handlers);

        /// <summary>
        /// Inserts a {@link ChannelHandler}s at the last position of this pipeline.
        ///
        /// @param invoker   the {@link ChannelHandlerInvoker} which invokes the {@code handler}s event handler methods
        /// @param handlers  the handlers to insert last
        ///
        /// </summary>
        IChannelPipeline AddLast(IChannelHandlerInvoker invoker, params IChannelHandler[] handlers);

        /// <summary>
        /// Removes the specified {@link ChannelHandler} from this pipeline.
        ///
        /// @param  handler          the {@link ChannelHandler} to remove
        ///
        /// @throws NoSuchElementException
        ///         if there's no such handler in this pipeline
        /// @throws NullPointerException
        ///         if the specified handler is {@code null}
        /// </summary>
        IChannelPipeline Remove(IChannelHandler handler);

        /// <summary>
        /// Removes the {@link ChannelHandler} with the specified name from this pipeline.
        /// </summary>
        /// <param name="name">the name under which the {@link ChannelHandler} was stored.</param>
        /// <returns>the removed handler</returns>
        /// 
        /// <exception cref="ArgumentException">if there's no such handler with the specified name in this pipeline</exception>
        /// <exception cref="ArgumentNullException">if the specified name is {@code null}</exception>
        IChannelHandler Remove(string name);

        /// <summary>
        /// Removes the {@link ChannelHandler} of the specified type from this pipeline.
        ///
        /// @param <T>           the type of the handler
        /// @param handlerType   the type of the handler
        ///
        /// @return the removed handler
        ///
        /// @throws NoSuchElementException
        ///         if there's no such handler of the specified type in this pipeline
        /// @throws NullPointerException
        ///         if the specified handler type is {@code null}
        /// </summary>
        T Remove<T>() where T : class, IChannelHandler;

        /// <summary>
        /// Removes the first {@link ChannelHandler} in this pipeline.
        ///
        /// @return the removed handler
        ///
        /// @throws NoSuchElementException
        ///         if this pipeline is empty
        /// </summary>
        IChannelHandler RemoveFirst();

        /// <summary>
        /// Removes the last {@link ChannelHandler} in this pipeline.
        ///
        /// @return the removed handler
        ///
        /// @throws NoSuchElementException
        ///         if this pipeline is empty
        /// </summary>
        IChannelHandler RemoveLast();

        /// <summary>
        /// Replaces the specified {@link ChannelHandler} with a new handler in this pipeline.
        ///
        /// @param  oldHandler    the {@link ChannelHandler} to be replaced
        /// @param  newName       the name under which the replacement should be added.
        ///                       {@code null} to use the same name with the handler being replaced.
        /// @param  newHandler    the {@link ChannelHandler} which is used as replacement
        ///
        /// @return itself
        /// @throws NoSuchElementException
        ///         if the specified old handler does not exist in this pipeline
        /// @throws IllegalArgumentException
        ///         if a handler with the specified new name already exists in this
        ///         pipeline, except for the handler to be replaced
        /// @throws NullPointerException
        ///         if the specified old handler or new handler is {@code null}
        /// </summary>
        IChannelPipeline Replace(IChannelHandler oldHandler, string newName, IChannelHandler newHandler);

        /// Replaces the {@link ChannelHandler} of the specified name with a new handler in this pipeline.
        /// @param  oldName       the name of the {@link ChannelHandler} to be replaced
        /// @param  newName       the name under which the replacement should be added.
        ///                       {@code null} to use the same name with the handler being replaced.
        /// @param  newHandler    the {@link ChannelHandler} which is used as replacement
        /// @return the removed handler
        /// @throws NoSuchElementException
        ///         if the handler with the specified old name does not exist in this pipeline
        /// @throws IllegalArgumentException
        ///         if a handler with the specified new name already exists in this
        ///         pipeline, except for the handler to be replaced
        /// @throws NullPointerException
        ///         if the specified old handler or new handler is {@code null}
        IChannelHandler Replace(string oldName, string newName, IChannelHandler newHandler);

        /// <summary>
        /// Replaces the {@link ChannelHandler} of the specified type with a new handler in this pipeline.
        ///
        /// @param  oldHandlerType   the type of the handler to be removed
        /// @param  newName          the name under which the replacement should be added.
        ///                          {@code null} to use the same name with the handler being replaced.
        /// @param  newHandler       the {@link ChannelHandler} which is used as replacement
        ///
        /// @return the removed handler
        ///
        /// @throws NoSuchElementException
        ///         if the handler of the specified old handler type does not exist
        ///         in this pipeline
        /// @throws IllegalArgumentException
        ///         if a handler with the specified new name already exists in this
        ///         pipeline, except for the handler to be replaced
        /// @throws NullPointerException
        ///         if the specified old handler or new handler is {@code null}
        /// </summary>
        T Replace<T>(string newName, IChannelHandler newHandler) where T : class, IChannelHandler;

        /// <summary>
        /// Returns the first {@link ChannelHandler} in this pipeline.
        ///
        /// @return the first handler.  {@code null} if this pipeline is empty.
        /// </summary>
        IChannelHandler First();

        /// <summary>
        /// Returns the context of the first {@link ChannelHandler} in this pipeline.
        ///
        /// @return the context of the first handler.  {@code null} if this pipeline is empty.
        /// </summary>
        IChannelHandlerContext FirstContext();

        /// <summary>
        /// Returns the last {@link ChannelHandler} in this pipeline.
        ///
        /// @return the last handler.  {@code null} if this pipeline is empty.
        /// </summary>
        IChannelHandler Last();

        /// <summary>
        /// Returns the context of the last {@link ChannelHandler} in this pipeline.
        ///
        /// @return the context of the last handler.  {@code null} if this pipeline is empty.
        /// </summary>
        IChannelHandlerContext LastContext();

        /// <summary>Returns the {@link ChannelHandler} with the specified name in this pipeline.</summary>
        /// <returns>the handler with the specified name. {@code null} if there's no such handler in this pipeline.</returns>
        IChannelHandler Get(string name);

        /// <summary>
        /// Returns the {@link ChannelHandler} of the specified type in this
        /// pipeline.
        ///
        /// @return the handler of the specified handler type.
        ///         {@code null} if there's no such handler in this pipeline.
        /// </summary>
        T Get<T>() where T : class, IChannelHandler;

        /// <summary>
        /// Returns the context object of the specified {@link ChannelHandler} in
        /// this pipeline.
        ///
        /// @return the context object of the specified handler.
        ///         {@code null} if there's no such handler in this pipeline.
        /// </summary>
        IChannelHandlerContext Context(IChannelHandler handler);

        /// <summary>Returns the context object of the {@link ChannelHandler} with the specified name in this pipeline.</summary>
        /// <returns>the context object of the handler with the specified name. {@code null} if there's no such handler in this pipeline.</returns>
        IChannelHandlerContext Context(string name);

        /// <summary>
        /// Returns the context object of the {@link ChannelHandler} of the
        /// specified type in this pipeline.
        ///
        /// @return the context object of the handler of the specified type.
        ///         {@code null} if there's no such handler in this pipeline.
        /// </summary>
        IChannelHandlerContext Context<T>() where T : class, IChannelHandler;

        /// <summary>
        /// Returns the {@link Channel} that this pipeline is attached to.
        ///
        /// @return the channel. {@code null} if this pipeline is not attached yet.
        /// </summary>
        IChannel Channel();

        /// <summary>
        /// A {@link Channel} is active now, which means it is connected.
        ///
        /// This will result in having the  {@link ChannelHandler#channelActive(ChannelHandlerContext)} method
        /// called of the next  {@link ChannelHandler} contained in the  {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        /// <summary>
        /// A {@link Channel} was registered to its {@link EventLoop}.
        ///
        /// This will result in having the  {@link ChannelHandler#channelRegistered(ChannelHandlerContext)} method
        /// called of the next  {@link ChannelHandler} contained in the  {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        IChannelPipeline FireChannelRegistered();

        /// <summary>
        /// A {@link Channel} was unregistered from its {@link EventLoop}.
        ///
        /// This will result in having the  {@link ChannelHandler#channelUnregistered(ChannelHandlerContext)} method
        /// called of the next  {@link ChannelHandler} contained in the  {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        IChannelPipeline FireChannelUnregistered();

        /// <summary>
        /// A {@link Channel} is active now, which means it is connected.
        ///
        /// This will result in having the  {@link ChannelHandler#channelActive(ChannelHandlerContext)} method
        /// called of the next  {@link ChannelHandler} contained in the  {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        IChannelPipeline FireChannelActive();

        /// <summary>
        /// A {@link Channel} is inactive now, which means it is closed.
        ///
        /// This will result in having the  {@link ChannelHandler#channelInactive(ChannelHandlerContext)} method
        /// called of the next  {@link ChannelHandler} contained in the  {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        IChannelPipeline FireChannelInactive();

        /// <summary>
        /// A {@link Channel} received an {@link Throwable} in one of its inbound operations.
        ///
        /// This will result in having the  {@link ChannelHandler#exceptionCaught(ChannelHandlerContext, Throwable)}
        /// method  called of the next  {@link ChannelHandler} contained in the  {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        IChannelPipeline FireExceptionCaught(Exception cause);

        /// <summary>
        /// A {@link Channel} received an user defined event.
        ///
        /// This will result in having the  {@link ChannelHandler#userEventTriggered(ChannelHandlerContext, Object)}
        /// method  called of the next  {@link ChannelHandler} contained in the  {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        IChannelPipeline FireUserEventTriggered(object evt);

        /// <summary>
        /// A {@link Channel} received a message.
        ///
        /// This will result in having the {@link ChannelHandler#channelRead(ChannelHandlerContext, Object)}
        /// method  called of the next {@link ChannelHandler} contained in the  {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        IChannelPipeline FireChannelRead(object msg);

        IChannelPipeline FireChannelReadComplete();

        /// <summary>
        /// Triggers an {@link ChannelHandler#channelWritabilityChanged(ChannelHandlerContext)}
        /// event to the next {@link ChannelHandler} in the {@link ChannelPipeline}.
        /// </summary>
        IChannelPipeline FireChannelWritabilityChanged();

        /// <summary>
        /// Request to bind to the given {@link SocketAddress} and notify the {@link ChannelFuture} once the operation
        /// completes, either because the operation was successful or because of an error.
        ///
        /// The given {@link ChannelPromise} will be notified.
        /// <p>
        /// This will result in having the
        /// {@link ChannelHandler#bind(ChannelHandlerContext, SocketAddress, ChannelPromise)} method
        /// called of the next {@link ChannelHandler} contained in the  {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        Task BindAsync(EndPoint localAddress);

        /// <summary>
        /// Request to connect to the given {@link EndPoint} and notify the {@link Task} once the operation
        /// completes, either because the operation was successful or because of an error.
        ///
        /// The given {@link Task} will be notified.
        ///
        /// <p>
        /// If the connection fails because of a connection timeout, the {@link Task} will get failed with
        /// a {@link ConnectTimeoutException}. If it fails because of connection refused a {@link ConnectException}
        /// will be used.
        /// <p>
        /// This will result in having the
        /// {@link ChannelHandler#connect(ChannelHandlerContext, EndPoint, EndPoint, ChannelPromise)}
        /// method called of the next {@link ChannelHandler} contained in the  {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        Task ConnectAsync(EndPoint remoteAddress);

        /// <summary>
        /// Request to connect to the given {@link EndPoint} while bind to the localAddress and notify the
        /// {@link Task} once the operation completes, either because the operation was successful or because of
        /// an error.
        ///
        /// The given {@link ChannelPromise} will be notified and also returned.
        /// <p>
        /// This will result in having the
        /// {@link ChannelHandler#connect(ChannelHandlerContext, EndPoint, EndPoint, ChannelPromise)}
        /// method called of the next {@link ChannelHandler} contained in the  {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress);

        /// <summary>
        /// Request to disconnect from the remote peer and notify the {@link Task} once the operation completes,
        /// either because the operation was successful or because of an error.
        ///
        /// The given {@link ChannelPromise} will be notified.
        /// <p>
        /// This will result in having the
        /// {@link ChannelHandler#disconnect(ChannelHandlerContext, ChannelPromise)}
        /// method called of the next {@link ChannelHandler} contained in the  {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Request to close the {@link Channel} and notify the {@link ChannelFuture} once the operation completes,
        /// either because the operation was successful or because of
        /// an error.
        ///
        /// After it is closed it is not possible to reuse it again.
        /// <p>
        /// This will result in having the
        /// {@link ChannelHandler#close(ChannelHandlerContext, ChannelPromise)}
        /// method called of the next {@link ChannelHandler} contained in the  {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        Task CloseAsync();

        /// <summary>
        /// Request to deregister the {@link Channel} bound this {@link ChannelPipeline} from the previous assigned
        /// {@link EventExecutor} and notify the {@link ChannelFuture} once the operation completes, either because the
        /// operation was successful or because of an error.
        ///
        /// The given {@link ChannelPromise} will be notified.
        /// <p>ChannelOutboundHandler
        /// This will result in having the
        /// {@link ChannelHandler#deregister(ChannelHandlerContext, ChannelPromise)}
        /// method called of the next {@link ChannelHandler} contained in the  {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        Task DeregisterAsync();

        /// <summary>
        /// Request to Read data from the {@link Channel} into the first inbound buffer, triggers an
        /// {@link ChannelHandler#channelRead(ChannelHandlerContext, Object)} event if data was
        /// read, and triggers a
        /// {@link ChannelHandler#channelReadComplete(ChannelHandlerContext) channelReadComplete} event so the
        /// handler can decide to continue reading.  If there's a pending read operation already, this method does nothing.
        /// <p>
        /// This will result in having the
        /// {@link ChannelHandler#read(ChannelHandlerContext)}
        /// method called of the next {@link ChannelHandler} contained in the  {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        IChannelPipeline Read();

        /// <summary>
        /// Request to write a message via this {@link ChannelPipeline}.
        /// This method will not request to actual flush, so be sure to call {@link #flush()}
        /// once you want to request to flush all pending data to the actual transport.
        /// </summary>
        Task WriteAsync(object msg);

        /// <summary>
        /// Request to flush all pending messages.
        /// </summary>
        IChannelPipeline Flush();

        /// <summary>
        /// Shortcut for call {@link #write(Object)} and {@link #flush()}.
        /// </summary>
        Task WriteAndFlushAsync(object msg);
    }
}