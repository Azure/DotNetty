// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;

    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;

    /// <summary>
    /// A list of <see cref="IChannelHandler"/>s which handles or intercepts inbound events and outbound operations of
    /// a <see cref="IChannel"/>. <see cref="IChannelPipeline"/> implements an advanced form of the
    /// <a href="http://www.oracle.com/technetwork/java/interceptingfilter-142169.html">Intercepting Filter</a> pattern
    /// to give a user full control over how an event is handled and how the <see cref="IChannelHandler"/>s in a
    /// pipeline interact with each other.
    /// <para>Creation of a pipeline</para>
    /// <para>Each channel has its own pipeline and it is created automatically when a new channel is created.</para>
    /// <para>How an event flows in a pipeline</para>
    /// <para>
    /// The following diagram describes how I/O events are processed by <see cref="IChannelHandler"/>s in a
    /// <see cref="IChannelPipeline"/> typically. An I/O event is handled by a <see cref="IChannelHandler"/> and is
    /// forwarded by the <see cref="IChannelHandler"/> which handled the event to the <see cref="IChannelHandler"/>
    /// which is placed right next to it. A <see cref="IChannelHandler"/> can also trigger an arbitrary I/O event if
    /// necessary. To forward or trigger an event, a <see cref="IChannelHandler"/> calls the event propagation methods
    /// defined in <see cref="IChannelHandlerContext"/>, such as <see cref="IChannelHandlerContext.FireChannelRead"/>
    /// and <see cref="IChannelHandlerContext.WriteAsync"/>.
    /// </para>
    /// <para>
    ///     <pre>
    ///         I/O Request
    ///         via <see cref="IChannel"/> or
    ///         {@link ChannelHandlerContext} 
    ///         |
    ///         +---------------------------------------------------+---------------+
    ///         |                           ChannelPipeline         |               |
    ///         |                                                  \|/              |
    ///         |    +----------------------------------------------+----------+    |
    ///         |    |                   ChannelHandler  N                     |    |
    ///         |    +----------+-----------------------------------+----------+    |
    ///         |              /|\                                  |               |
    ///         |               |                                  \|/              |
    ///         |    +----------+-----------------------------------+----------+    |
    ///         |    |                   ChannelHandler N-1                    |    |
    ///         |    +----------+-----------------------------------+----------+    |
    ///         |              /|\                                  .               |
    ///         |               .                                   .               |
    ///         | ChannelHandlerContext.fireIN_EVT() ChannelHandlerContext.OUT_EVT()|
    ///         |          [method call]                      [method call]         |
    ///         |               .                                   .               |
    ///         |               .                                  \|/              |
    ///         |    +----------+-----------------------------------+----------+    |
    ///         |    |                   ChannelHandler  2                     |    |
    ///         |    +----------+-----------------------------------+----------+    |
    ///         |              /|\                                  |               |
    ///         |               |                                  \|/              |
    ///         |    +----------+-----------------------------------+----------+    |
    ///         |    |                   ChannelHandler  1                     |    |
    ///         |    +----------+-----------------------------------+----------+    |
    ///         |              /|\                                  |               |
    ///         +---------------+-----------------------------------+---------------+
    ///         |                                  \|/
    ///         +---------------+-----------------------------------+---------------+
    ///         |               |                                   |               |
    ///         |       [ Socket.read() ]                    [ Socket.write() ]     |
    ///         |                                                                   |
    ///         |  Netty Internal I/O Threads (Transport Implementation)            |
    ///         +-------------------------------------------------------------------+
    ///     </pre>
    /// </para>
    /// <para>
    /// An inbound event is handled by the <see cref="IChannelHandler"/>s in the bottom-up direction as shown on the
    /// left side of the diagram. An inbound event is usually triggered by the I/O thread on the bottom of the diagram
    /// so that the <see cref="IChannelHandler"/>s are notified when the state of a <see cref="IChannel"/> changes
    /// (e.g. newly established connections and closed connections) or the inbound data was read from a remote peer. If
    /// an inbound event goes beyond the <see cref="IChannelHandler"/> at the top of the diagram, it is discarded and
    /// logged, depending on your loglevel.
    /// </para>
    /// <para>
    /// An outbound event is handled by the <see cref="IChannelHandler"/>s in the top-down direction as shown on the
    /// right side of the diagram. An outbound event is usually triggered by your code that requests an outbound I/O
    /// operation, such as a write request and a connection attempt.  If an outbound event goes beyond the
    /// <see cref="IChannelHandler"/> at the bottom of the diagram, it is handled by an I/O thread associated with the
    /// <see cref="IChannel"/>. The I/O thread often performs the actual output operation such as
    /// <see cref="AbstractChannel.WriteAsync"/>.
    /// </para>
    /// <para>Forwarding an event to the next handler</para>
    /// <para>
    /// As explained briefly above, a <see cref="IChannelHandler"/> has to invoke the event propagation methods in
    /// <see cref="IChannelHandlerContext"/> to forward an event to its next handler. Those methods include:
    ///     <ul>
    ///         <li>
    ///             Inbound event propagation methods:
    ///             <ul>
    ///                 <li><see cref="IChannelHandlerContext.FireChannelRegistered"/></li>
    ///                 <li><see cref="IChannelHandlerContext.FireChannelActive"/></li>
    ///                 <li><see cref="IChannelHandlerContext.FireChannelRead"/></li>
    ///                 <li><see cref="IChannelHandlerContext.FireChannelReadComplete"/></li>
    ///                 <li><see cref="IChannelHandlerContext.FireExceptionCaught"/></li>
    ///                 <li><see cref="IChannelHandlerContext.FireUserEventTriggered"/></li>
    ///                 <li><see cref="IChannelHandlerContext.FireChannelWritabilityChanged"/></li>
    ///                 <li><see cref="IChannelHandlerContext.FireChannelInactive"/></li>
    ///             </ul>
    ///         </li>
    ///         <li>
    ///             Outbound event propagation methods:
    ///             <ul>
    ///                 <li><see cref="IChannelHandlerContext.BindAsync"/></li>
    ///                 <li><see cref="IChannelHandlerContext.ConnectAsync(EndPoint)"/></li>
    ///                 <li><see cref="IChannelHandlerContext.ConnectAsync(EndPoint, EndPoint)"/></li>
    ///                 <li><see cref="IChannelHandlerContext.WriteAsync"/></li>
    ///                 <li><see cref="IChannelHandlerContext.Flush"/></li>
    ///                 <li><see cref="IChannelHandlerContext.Read"/></li>
    ///                 <li><see cref="IChannelHandlerContext.DisconnectAsync"/></li>
    ///                 <li><see cref="IChannelHandlerContext.CloseAsync"/></li>
    ///             </ul>
    ///         </li>
    ///     </ul>
    /// </para>
    /// <para>
    ///     and the following example shows how the event propagation is usually done:
    ///     <code>
    ///         public class MyInboundHandler : <see cref="ChannelHandlerAdapter"/>
    ///         {
    ///             public override void ChannelActive(<see cref="IChannelHandlerContext"/> ctx)
    ///             {
    ///                 Console.WriteLine("Connected!");
    ///                 ctx.FireChannelActive();
    ///             }
    ///         }
    /// 
    ///         public class MyOutboundHandler : <see cref="ChannelHandlerAdapter"/>
    ///         {
    ///             public override async Task CloseAsync(<see cref="IChannelHandlerContext"/> ctx)
    ///             {
    ///                 Console.WriteLine("Closing...");
    ///                 await ctx.CloseAsync();
    ///             }
    ///         }
    ///     </code>
    /// </para>
    /// <para>Building a pipeline</para>
    /// <para>
    /// A user is supposed to have one or more <see cref="IChannelHandler"/>s in a pipeline to receive I/O events
    /// (e.g. read) and to request I/O operations (e.g. write and close).  For example, a typical server will have the
    /// following handlers in each channel's pipeline, but your mileage may vary depending on the complexity and
    /// characteristics of the protocol and business logic:
    ///     <ol>
    ///         <li>Protocol Decoder - translates binary data (e.g. <see cref="IByteBuffer"/>) into a Java object.</li>
    ///         <li>Protocol Encoder - translates a Java object into binary data.</li>
    ///         <li>Business Logic Handler - performs the actual business logic (e.g. database access).</li>
    ///     </ol>
    /// </para>
    /// <para>
    ///     and it could be represented as shown in the following example:
    ///     <code>
    ///         static readonly <see cref="IEventExecutorGroup"/> group = new <see cref="MultithreadEventLoopGroup"/>();
    ///         ...
    ///         <see cref="IChannelPipeline"/> pipeline = ch.Pipeline;
    ///         pipeline.AddLast("decoder", new MyProtocolDecoder());
    ///         pipeline.AddLast("encoder", new MyProtocolEncoder());
    /// 
    ///         // Tell the pipeline to run MyBusinessLogicHandler's event handler methods
    ///         // in a different thread than an I/O thread so that the I/O thread is not blocked by
    ///         // a time-consuming task.
    ///         // If your business logic is fully asynchronous or finished very quickly, you don't
    ///         // need to specify a group.
    ///         pipeline.AddLast(group, "handler", new MyBusinessLogicHandler());
    ///     </code>
    /// </para>
    /// <para>Thread safety</para>
    /// <para>
    /// An <see cref="IChannelHandler"/> can be added or removed at any time because an <see cref="IChannelPipeline"/>
    /// is thread safe. For example, you can insert an encryption handler when sensitive information is about to be
    /// exchanged, and remove it after the exchange.
    /// </para>
    /// </summary>
    public interface IChannelPipeline : IEnumerable<IChannelHandler>
    {
        /// <summary>
        /// Inserts an <see cref="IChannelHandler"/> at the first position of this pipeline.
        /// </summary>
        /// <param name="name">
        /// The name of the handler to insert first. Pass <c>null</c> to let the name auto-generated.
        /// </param>
        /// <param name="handler">The <see cref="IChannelHandler"/> to insert first.</param>
        /// <returns>The <see cref="IChannelPipeline"/>.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if an entry with the same <paramref name="name"/> already exists.
        /// </exception>
        /// <exception cref="ArgumentNullException">Thrown if the specified handler is <c>null</c>.</exception>
        IChannelPipeline AddFirst(string name, IChannelHandler handler);

        /// <summary>
        /// Inserts a <see cref="IChannelHandler"/> at the first position of this pipeline.
        /// </summary>
        /// <param name="group">
        /// The <see cref="IEventExecutorGroup"/> which invokes the <paramref name="handler"/>'s event handler methods.
        /// </param>
        /// <param name="name">
        /// The name of the handler to insert first. Pass <c>null</c> to let the name be auto-generated.
        /// </param>
        /// <param name="handler">The <see cref="IChannelHandler"/> to insert first.</param>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if an entry with the same <paramref name="name"/> already exists.
        /// </exception>
        /// <exception cref="ArgumentNullException">Thrown if the specified handler is <c>null</c>.</exception>
        IChannelPipeline AddFirst(IEventExecutorGroup group, string name, IChannelHandler handler);

        /// <summary>
        /// Appends an <see cref="IChannelHandler"/> at the last position of this pipeline.
        /// </summary>
        /// <param name="name">
        /// The name of the handler to append. Pass <c>null</c> to let the name be auto-generated.
        /// </param>
        /// <param name="handler">The <see cref="IChannelHandler"/> to append.</param>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if an entry with the same <paramref name="name"/> already exists.
        /// </exception>
        /// <exception cref="ArgumentNullException">Thrown if the specified handler is <c>null</c>.</exception>
        IChannelPipeline AddLast(string name, IChannelHandler handler);

        /// <summary>
        /// Appends a <see cref="IChannelHandler"/> at the last position of this pipeline.
        /// </summary>
        /// <param name="group">
        /// The <see cref="IEventExecutorGroup"/> which invokes the <paramref name="handler"/>'s event handler methods.
        /// </param>
        /// <param name="name">
        /// The name of the handler to append. Pass <c>null</c> to let the name be auto-generated.
        /// </param>
        /// <param name="handler">The <see cref="IChannelHandler"/> to append.</param>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if an entry with the same <paramref name="name"/> already exists.
        /// </exception>
        /// <exception cref="ArgumentNullException">Thrown if the specified handler is <c>null</c>.</exception>
        IChannelPipeline AddLast(IEventExecutorGroup group, string name, IChannelHandler handler);

        /// <summary>
        /// Inserts a <see cref="IChannelHandler"/> before an existing handler of this pipeline.
        /// </summary>
        /// <param name="baseName">The name of the existing handler.</param>
        /// <param name="name">
        /// The name of the new handler being appended. Pass <c>null</c> to let the name be auto-generated.
        /// </param>
        /// <param name="handler">The <see cref="IChannelHandler"/> to append.</param>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if an entry with the same <paramref name="name"/> already exists, or if no match was found for the
        /// given <paramref name="baseName"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">Thrown if the specified handler is <c>null</c>.</exception>
        IChannelPipeline AddBefore(string baseName, string name, IChannelHandler handler);

        /// <summary>
        /// Inserts a <see cref="IChannelHandler"/> before an existing handler of this pipeline.
        /// </summary>
        /// <param name="group">
        /// The <see cref="IEventExecutorGroup"/> which invokes the <paramref name="handler"/>'s event handler methods.
        /// </param>
        /// <param name="baseName">The name of the existing handler.</param>
        /// <param name="name">
        /// The name of the new handler being appended. Pass <c>null</c> to let the name be auto-generated.
        /// </param>
        /// <param name="handler">The <see cref="IChannelHandler"/> to append.</param>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if an entry with the same <paramref name="name"/> already exists, or if no match was found for the
        /// given <paramref name="baseName"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">Thrown if the specified handler is <c>null</c>.</exception>
        IChannelPipeline AddBefore(IEventExecutorGroup group, string baseName, string name, IChannelHandler handler);

        /// <summary>
        /// Inserts a <see cref="IChannelHandler"/> after an existing handler of this pipeline.
        /// </summary>
        /// <param name="baseName">The name of the existing handler.</param>
        /// <param name="name">
        /// The name of the new handler being appended. Pass <c>null</c> to let the name be auto-generated.
        /// </param>
        /// <param name="handler">The handler to insert after.</param>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if an entry with the same <paramref name="name"/> already exists, or if no match was found for the
        /// given <paramref name="baseName"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">Thrown if the specified handler is <c>null</c>.</exception>
        IChannelPipeline AddAfter(string baseName, string name, IChannelHandler handler);

        /// <summary>
        /// Inserts a <see cref="IChannelHandler"/> after an existing handler of this pipeline.
        /// </summary>
        /// <param name="group">
        /// The <see cref="IEventExecutorGroup"/> which invokes the <paramref name="handler"/>'s event handler methods.
        /// </param>
        /// <param name="baseName">The name of the existing handler.</param>
        /// <param name="name">
        /// The name of the new handler being appended. Pass <c>null</c> to let the name be auto-generated.
        /// </param>
        /// <param name="handler">The handler to insert after.</param>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if an entry with the same <paramref name="name"/> already exists, or if no match was found for the
        /// given <paramref name="baseName"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">Thrown if the specified handler is <c>null</c>.</exception>
        IChannelPipeline AddAfter(IEventExecutorGroup group, string baseName, string name, IChannelHandler handler);

        /// <summary>
        /// Inserts multiple <see cref="IChannelHandler"/>s at the first position of this pipeline.
        /// </summary>
        /// <param name="handlers">The <see cref="IChannelHandler"/>s to insert.</param>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        IChannelPipeline AddFirst(params IChannelHandler[] handlers);

        /// <summary>
        /// Inserts multiple <see cref="IChannelHandler"/>s at the first position of this pipeline.
        /// </summary>
        /// <param name="group">
        /// The <see cref="IEventExecutorGroup"/> which invokes the <paramref name="handlers"/>' event handler methods.
        /// </param>
        /// <param name="handlers">The <see cref="IChannelHandler"/>s to insert.</param>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        IChannelPipeline AddFirst(IEventExecutorGroup group, params IChannelHandler[] handlers);

        /// <summary>
        /// Inserts multiple <see cref="IChannelHandler"/>s at the last position of this pipeline.
        /// </summary>
        /// <param name="handlers">The <see cref="IChannelHandler"/>s to insert.</param>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        IChannelPipeline AddLast(params IChannelHandler[] handlers);

        /// <summary>
        /// Inserts multiple <see cref="IChannelHandler"/>s at the last position of this pipeline.
        /// </summary>
        /// <param name="group">
        /// The <see cref="IEventExecutorGroup"/> which invokes the <paramref name="handlers"/>' event handler methods.
        /// </param>
        /// <param name="handlers">The <see cref="IChannelHandler"/>s to insert.</param>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        IChannelPipeline AddLast(IEventExecutorGroup group, params IChannelHandler[] handlers);

        /// <summary>
        /// Removes the specified <see cref="IChannelHandler"/> from this pipeline.
        /// </summary>
        /// <param name="handler">The <see cref="IChannelHandler"/> to remove.</param>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        /// <exception cref="ArgumentException">Thrown if the specified handler was not found.</exception>
        IChannelPipeline Remove(IChannelHandler handler);

        /// <summary>
        /// Removes the <see cref="IChannelHandler"/> with the specified name from this pipeline.
        /// </summary>
        /// <param name="name">The name under which the <see cref="IChannelHandler"/> was stored.</param>
        /// <returns>The removed <see cref="IChannelHandler"/>.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if there's no such handler with the specified name in this pipeline.
        /// </exception>
        IChannelHandler Remove(string name);

        /// <summary>
        /// Removes the <see cref="IChannelHandler"/> of the specified type from this pipeline.
        /// </summary>
        /// <typeparam name="T">The type of handler to remove.</typeparam>
        /// <returns>The removed <see cref="IChannelHandler"/>.</returns>
        /// <exception cref="ArgumentException">Thrown if there's no handler of the specified type in this pipeline.</exception>
        T Remove<T>() where T : class, IChannelHandler;

        /// <summary>
        /// Removes the first <see cref="IChannelHandler"/> in this pipeline.
        /// </summary>
        /// <returns>The removed <see cref="IChannelHandler"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if this pipeline is empty.</exception>
        IChannelHandler RemoveFirst();

        /// <summary>
        /// Removes the last <see cref="IChannelHandler"/> in this pipeline.
        /// </summary>
        /// <returns>The removed <see cref="IChannelHandler"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if this pipeline is empty.</exception>
        IChannelHandler RemoveLast();

        /// <summary>
        /// Replaces the specified <see cref="IChannelHandler"/> with a new handler in this pipeline.
        /// </summary>
        /// <param name="oldHandler">The <see cref="IChannelHandler"/> to be replaced.</param>
        /// <param name="newName">
        /// The name of the new handler being inserted. Pass <c>null</c> to let the name be auto-generated.
        /// </param>
        /// <param name="newHandler">The new <see cref="IChannelHandler"/> to be inserted.</param>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if an entry with the same <paramref name="newName"/> already exists, or if the
        /// <paramref name="oldHandler"/> was not found.
        /// </exception>
        IChannelPipeline Replace(IChannelHandler oldHandler, string newName, IChannelHandler newHandler);

        /// <summary>
        /// Replaces the <see cref="IChannelHandler"/> of the specified name with a new handler in this pipeline.
        /// </summary>
        /// <param name="oldName">The name of the <see cref="IChannelHandler"/> to be replaced.</param>
        /// <param name="newName">
        /// The name of the new handler being inserted. Pass <c>null</c> to let the name be auto-generated.
        /// </param>
        /// <param name="newHandler">The new <see cref="IChannelHandler"/> to be inserted.</param>
        /// <returns>The <see cref="IChannelHandler"/> that was replaced.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if an entry with the same <paramref name="newName"/> already exists, or if no match was found for
        /// the given <paramref name="oldName"/>.
        /// </exception>
        IChannelHandler Replace(string oldName, string newName, IChannelHandler newHandler);

        /// <summary>
        /// Replaces the <see cref="IChannelHandler"/> of the specified type with a new handler in this pipeline.
        /// </summary>
        /// <typeparam name="T">The type of the handler to be removed.</typeparam>
        /// <param name="newName">
        /// The name of the new handler being inserted. Pass <c>null</c> to let the name be auto-generated.
        /// </param>
        /// <param name="newHandler">The new <see cref="IChannelHandler"/> to be inserted.</param>
        /// <returns>The <see cref="IChannelHandler"/> that was replaced.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if an entry with the same <paramref name="newName"/> already exists, or if no match was found for
        /// the given type.
        /// </exception>
        T Replace<T>(string newName, IChannelHandler newHandler) where T : class, IChannelHandler;

        /// <summary>
        /// Returns the first <see cref="IChannelHandler"/> in this pipeline.
        /// </summary>
        /// <returns>The first handler in the pipeline, or <c>null</c> if the pipeline is empty.</returns>
        IChannelHandler First();

        /// <summary>
        /// Returns the context of the first <see cref="IChannelHandler"/> in this pipeline.
        /// </summary>
        /// <returns>
        /// The context of the first handler in the pipeline, or <c>null</c> if the pipeline is empty.
        /// </returns>
        IChannelHandlerContext FirstContext();

        /// <summary>
        /// Returns the last <see cref="IChannelHandler"/> in this pipeline.
        /// </summary>
        /// <returns>The last handler in the pipeline, or <c>null</c> if the pipeline is empty.</returns>
        IChannelHandler Last();

        /// <summary>
        /// Returns the context of the last <see cref="IChannelHandler"/> in this pipeline.
        /// </summary>
        /// <returns>
        /// The context of the last handler in the pipeline, or <c>null</c> if the pipeline is empty.
        /// </returns>
        IChannelHandlerContext LastContext();

        /// <summary>
        /// Returns the <see cref="IChannelHandler"/> with the specified name in this pipeline.
        /// </summary>
        /// <param name="name">The name of the desired <see cref="IChannelHandler"/>.</param>
        /// <returns>
        /// The handler with the specified name, or <c>null</c> if there's no such handler in this pipeline.
        /// </returns>
        IChannelHandler Get(string name);

        /// <summary>
        /// Returns the <see cref="IChannelHandler"/> of the specified type in this pipeline.
        /// </summary>
        /// <typeparam name="T">The type of handler to retrieve.</typeparam>
        /// <returns>
        /// The handler with the specified type, or <c>null</c> if there's no such handler in this pipeline.
        /// </returns>
        T Get<T>() where T : class, IChannelHandler;

        /// <summary>
        /// Returns the context object of the specified <see cref="IChannelHandler"/> in this pipeline.
        /// </summary>
        /// <param name="handler">The <see cref="IChannelHandler"/> whose context should be retrieved.</param>
        /// <returns>
        /// The context object of the specified handler, or <c>null</c> if there's no such handler in this pipeline.
        /// </returns>
        IChannelHandlerContext Context(IChannelHandler handler);

        /// <summary>
        /// Returns the context object of the <see cref="IChannelHandler"/> with the specified name in this pipeline.
        /// </summary>
        /// <param name="name">The name of the <see cref="IChannelHandler"/> whose context should be retrieved.</param>
        /// <returns>
        /// The context object of the handler with the specified name, or <c>null</c> if there's no such handler in
        /// this pipeline.
        /// </returns>
        IChannelHandlerContext Context(string name);

        /// <summary>
        /// Returns the context object of the <see cref="IChannelHandler"/> of the specified type in this pipeline.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IChannelHandler"/> whose context should be retrieved.</typeparam>
        /// <returns>
        /// The context object of the handler with the specified type, or <c>null</c> if there's no such handler in
        /// this pipeline.
        /// </returns>
        IChannelHandlerContext Context<T>() where T : class, IChannelHandler;

        /// <summary>
        /// Returns the <see cref="IChannel" /> that this pipeline is attached to.
        /// Returns <c>null</c> if this pipeline is not attached to any channel yet.
        /// </summary>
        IChannel Channel { get; }

        /// <summary>
        /// An <see cref="IChannel"/> was registered to its <see cref="IEventLoop"/>.
        /// This will result in having the <see cref="IChannelHandler.ChannelRegistered"/> method
        /// called of the next <see cref="IChannelHandler"/> contained in the <see cref="IChannelPipeline"/> of the
        /// <see cref="IChannel"/>.
        /// </summary>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        IChannelPipeline FireChannelRegistered();

        /// <summary>
        /// An <see cref="IChannel"/> was unregistered from its <see cref="IEventLoop"/>.
        /// This will result in having the <see cref="IChannelHandler.ChannelUnregistered"/> method
        /// called of the next <see cref="IChannelHandler"/> contained in the <see cref="IChannelPipeline"/> of the
        /// <see cref="IChannel"/>.
        /// </summary>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        IChannelPipeline FireChannelUnregistered();

        /// <summary>
        /// An <see cref="IChannel"/> is active now, which means it is connected.
        /// This will result in having the <see cref="IChannelHandler.ChannelActive"/> method
        /// called of the next <see cref="IChannelHandler"/> contained in the <see cref="IChannelPipeline"/> of the
        /// <see cref="IChannel"/>.
        /// </summary>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        IChannelPipeline FireChannelActive();

        /// <summary>
        /// An <see cref="IChannel"/> is inactive now, which means it is closed.
        /// This will result in having the <see cref="IChannelHandler.ChannelInactive"/> method
        /// called of the next <see cref="IChannelHandler"/> contained in the <see cref="IChannelPipeline"/> of the
        /// <see cref="IChannel"/>.
        /// </summary>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        IChannelPipeline FireChannelInactive();

        /// <summary>
        /// An <see cref="IChannel"/> received an <see cref="Exception"/> in one of its inbound operations.
        /// This will result in having the <see cref="IChannelHandler.ExceptionCaught"/> method
        /// called of the next <see cref="IChannelHandler"/> contained in the <see cref="IChannelPipeline"/> of the
        /// <see cref="IChannel"/>.
        /// </summary>
        /// <param name="cause">The <see cref="Exception"/> that was caught.</param>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        IChannelPipeline FireExceptionCaught(Exception cause);

        /// <summary>
        /// An <see cref="IChannel"/> received an user defined event.
        /// This will result in having the <see cref="IChannelHandler.UserEventTriggered"/> method
        /// called of the next <see cref="IChannelHandler"/> contained in the <see cref="IChannelPipeline"/> of the
        /// <see cref="IChannel"/>.
        /// </summary>
        /// <param name="evt">The user-defined event that was triggered.</param>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        IChannelPipeline FireUserEventTriggered(object evt);

        /// <summary>
        /// An <see cref="IChannel"/> received a message.
        /// This will result in having the <see cref="IChannelHandler.ChannelRead"/> method
        /// called of the next <see cref="IChannelHandler"/> contained in the <see cref="IChannelPipeline"/> of the
        /// <see cref="IChannel"/>.
        /// </summary>
        /// <param name="msg">The message that was received.</param>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        IChannelPipeline FireChannelRead(object msg);

        /// <summary>
        /// An <see cref="IChannel"/> completed a message after reading it.
        /// This will result in having the <see cref="IChannelHandler.ChannelReadComplete"/> method
        /// called of the next <see cref="IChannelHandler"/> contained in the <see cref="IChannelPipeline"/> of the
        /// <see cref="IChannel"/>.
        /// </summary>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        IChannelPipeline FireChannelReadComplete();

        /// <summary>
        /// Triggers an <see cref="IChannelHandler.ChannelWritabilityChanged"/> event to the next
        /// <see cref="IChannelHandler"/> in the <see cref="IChannelPipeline"/>.
        /// </summary>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        IChannelPipeline FireChannelWritabilityChanged();

        /// <summary>
        /// Request to bind to the given <see cref="EndPoint"/>.
        /// <para>
        /// This will result in having the <see cref="IChannelHandler.BindAsync"/> method called of the next
        /// <see cref="IChannelHandler"/> contained in the  <see cref="IChannelPipeline"/> of the
        /// <see cref="IChannel"/>.
        /// </para>
        /// </summary>
        Task BindAsync(EndPoint localAddress);

        /// <summary>
        /// Request to connect to the given <see cref="EndPoint"/>.
        /// <para>
        /// This will result in having the <see cref="IChannelHandler.ConnectAsync"/> method called of the next
        /// <see cref="IChannelHandler"/> contained in the  <see cref="IChannelPipeline"/> of the
        /// <see cref="IChannel"/>.
        /// </para>
        /// </summary>
        /// <param name="remoteAddress">The remote <see cref="EndPoint"/> to connect to.</param>
        /// <returns>An await-able task.</returns>
        Task ConnectAsync(EndPoint remoteAddress);

        /// <summary>
        /// Request to connect to the given <see cref="EndPoint"/>.
        /// <para>
        /// This will result in having the <see cref="IChannelHandler.ConnectAsync"/> method called of the next
        /// <see cref="IChannelHandler"/> contained in the  <see cref="IChannelPipeline"/> of the
        /// <see cref="IChannel"/>.
        /// </para>
        /// </summary>
        /// <param name="remoteAddress">The remote <see cref="EndPoint"/> to connect to.</param>
        /// <param name="localAddress">The local <see cref="EndPoint"/> to bind.</param>
        /// <returns>An await-able task.</returns>
        Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress);

        /// <summary>
        /// Request to disconnect from the remote peer.
        /// <para>
        /// This will result in having the <see cref="IChannelHandler.DisconnectAsync"/> method called of the next
        /// <see cref="IChannelHandler"/> contained in the  <see cref="IChannelPipeline"/> of the
        /// <see cref="IChannel"/>.
        /// </para>
        /// </summary>
        /// <returns>An await-able task.</returns>
        Task DisconnectAsync();

        /// <summary>
        /// Request to close the <see cref="IChannel"/>. After it is closed it is not possible to reuse it again.
        /// <para>
        /// This will result in having the <see cref="IChannelHandler.CloseAsync"/> method called of the next
        /// <see cref="IChannelHandler"/> contained in the  <see cref="IChannelPipeline"/> of the
        /// <see cref="IChannel"/>.
        /// </para>
        /// </summary>
        /// <returns>An await-able task.</returns>
        Task CloseAsync();

        /// <summary>
        /// Request to deregister the <see cref="IChannel"/> bound this <see cref="IChannelPipeline"/> from the
        /// previous assigned <see cref="IEventExecutor"/>.
        /// <para>
        /// This will result in having the <see cref="IChannelHandler.DeregisterAsync"/> method called of the next
        /// <see cref="IChannelHandler"/> contained in the  <see cref="IChannelPipeline"/> of the
        /// <see cref="IChannel"/>.
        /// </para>
        /// </summary>
        /// <returns>An await-able task.</returns>
        Task DeregisterAsync();

        /// <summary>
        /// Request to Read data from the <see cref="IChannel"/> into the first inbound buffer, triggers an
        /// <see cref="IChannelHandler.ChannelRead"/> event if data was read, and triggers a
        /// <see cref="IChannelHandler.ChannelReadComplete"/> event so the handler can decide whether to continue
        /// reading. If there's a pending read operation already, this method does nothing.
        /// <para>
        /// This will result in having the <see cref="IChannelHandler.Read"/> method called of the next
        /// <see cref="IChannelHandler"/> contained in the  <see cref="IChannelPipeline"/> of the
        /// <see cref="IChannel"/>.
        /// </para>
        /// </summary>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        IChannelPipeline Read();

        /// <summary>
        /// Request to write a message via this <see cref="IChannelPipeline"/>.
        /// This method will not request to actual flush, so be sure to call <see cref="Flush"/>
        /// once you want to request to flush all pending data to the actual transport.
        /// </summary>
        /// <returns>An await-able task.</returns>
        Task WriteAsync(object msg);

        /// <summary>
        /// Request to flush all pending messages.
        /// </summary>
        /// <returns>This <see cref="IChannelPipeline"/>.</returns>
        IChannelPipeline Flush();

        /// <summary>
        /// Shortcut for calling both <see cref="WriteAsync"/> and <see cref="Flush"/>.
        /// </summary>
        Task WriteAndFlushAsync(object msg);
    }
}