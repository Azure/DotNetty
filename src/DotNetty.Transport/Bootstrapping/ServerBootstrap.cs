// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Bootstrapping
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    ///     {@link Bootstrap} sub-class which allows easy bootstrap of {@link ServerChannel}
    /// </summary>
    public class ServerBootstrap : AbstractBootstrap<ServerBootstrap, IServerChannel>
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<ServerBootstrap>();

        readonly ConcurrentDictionary<ChannelOption, object> childOptions;
        // todo: attrs
        //private final Map<AttributeKey<?>, Object> childAttrs = new LinkedHashMap<AttributeKey<?>, Object>();
        volatile IEventLoopGroup childGroup;
        volatile IChannelHandler childHandler;

        public ServerBootstrap()
        {
            this.childOptions = new ConcurrentDictionary<ChannelOption, object>();
        }

        ServerBootstrap(ServerBootstrap bootstrap)
            : base(bootstrap)
        {
            this.childGroup = bootstrap.childGroup;
            this.childHandler = bootstrap.childHandler;
            this.childOptions = new ConcurrentDictionary<ChannelOption, object>(bootstrap.childOptions);
            // todo: attrs
            //lock (bootstrap.childAttrs) {
            //    childAttrs.putAll(bootstrap.childAttrs);
            //}
        }

        /// <summary>
        ///     Specify the {@link EventLoopGroup} which is used for the parent (acceptor) and the child (client).
        /// </summary>
        public override ServerBootstrap Group(IEventLoopGroup group) => this.Group(@group, @group);

        /// <summary>
        ///     Set the {@link EventLoopGroup} for the parent (acceptor) and the child (client). These
        ///     {@link EventLoopGroup}'s are used to handle all the events and IO for {@link ServerChannel} and
        ///     {@link Channel}'s.
        /// </summary>
        public ServerBootstrap Group(IEventLoopGroup parentGroup, IEventLoopGroup childGroup)
        {
            Contract.Requires(childGroup != null);

            base.Group(parentGroup);
            if (this.childGroup != null)
            {
                throw new InvalidOperationException("childGroup set already");
            }
            this.childGroup = childGroup;
            return this;
        }

        /// <summary>
        ///     Allow to specify a {@link ChannelOption} which is used for the {@link Channel} instances once they get created
        ///     (after the acceptor accepted the {@link Channel}). Use a value of {@code null} to remove a previous set
        ///     {@link ChannelOption}.
        /// </summary>
        public ServerBootstrap ChildOption<T>(ChannelOption<T> childOption, T value)
        {
            Contract.Requires(childOption != null);

            if (value == null)
            {
                object removed;
                this.childOptions.TryRemove(childOption, out removed);
            }
            else
            {
                this.childOptions[childOption] = value;
            }
            return this;
        }

        // todo: attrs
        ///// <summary>
        // /// Set the specific {@link AttributeKey} with the given value on every child {@link Channel}. If the value is
        // /// {@code null} the {@link AttributeKey} is removed
        // /// </summary>
        //public <T> ServerBootstrap childAttr(AttributeKey<T> childKey, T value) {
        //    if (childKey == null) {
        //        throw new NullPointerException("childKey");
        //    }
        //    if (value == null) {
        //        childAttrs.remove(childKey);
        //    } else {
        //        childAttrs.put(childKey, value);
        //    }
        //    return this;
        //}
        /// <summary>
        ///     Set the {@link ChannelHandler} which is used to serve the request for the {@link Channel}'s.
        /// </summary>
        public ServerBootstrap ChildHandler(IChannelHandler childHandler)
        {
            Contract.Requires(childHandler != null);

            this.childHandler = childHandler;
            return this;
        }

        /// <summary>
        ///     Return the configured {@link EventLoopGroup} which will be used for the child channels or {@code null}
        ///     if non is configured yet.
        /// </summary>
        public IEventLoopGroup ChildGroup() => this.childGroup;

        protected override void Init(IChannel channel)
        {
            IDictionary<ChannelOption, object> options = this.Options();
            foreach (KeyValuePair<ChannelOption, object> e in options)
            {
                try
                {
                    if (!channel.Configuration.SetOption(e.Key, e.Value))
                    {
                        Logger.Warn("Unknown channel option: " + e);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn("Failed to set a channel option: " + channel, ex);
                }
            }

            // todo: attrs
            //Map<AttributeKey<?>, Object> attrs = attrs();
            //lock (attrs) {
            //    foreach (var e in attrs.entrySet()) {
            //        AttributeKey<object> key = (AttributeKey<object>) e.getKey();
            //        channel.attr(key).set(e.getValue());
            //    }
            //}

            IChannelPipeline p = channel.Pipeline;
            if (this.Handler() != null)
            {
                p.AddLast(this.Handler());
            }

            IEventLoopGroup currentChildGroup = this.childGroup;
            IChannelHandler currentChildHandler = this.childHandler;
            KeyValuePair<ChannelOption, object>[] currentChildOptions = this.childOptions.ToArray();
            // todo: attrs
            //Entry<AttributeKey<?>, Object>[] currentChildAttrs = this.childAttrs.ToArray();

            Func<IChannelConfiguration, bool> childConfigSetupFunc = CompileOptionsSetupFunc(this.childOptions);

            p.AddLast(new ActionChannelInitializer<IChannel>(ch =>
            {
                ch.Pipeline.AddLast(new ServerBootstrapAcceptor(currentChildGroup, currentChildHandler,
                    childConfigSetupFunc /*, currentChildAttrs*/));
            }));
        }

        public override ServerBootstrap Validate()
        {
            base.Validate();
            if (this.childHandler == null)
            {
                throw new InvalidOperationException("childHandler not set");
            }
            if (this.childGroup == null)
            {
                Logger.Warn("childGroup is not set. Using parentGroup instead.");
                this.childGroup = this.Group();
            }
            return this;
        }

        static Func<IChannelConfiguration, bool> CompileOptionsSetupFunc(IDictionary<ChannelOption, object> templateOptions)
        {
            if (templateOptions.Count == 0)
            {
                return null;
            }

            ParameterExpression configParam = Expression.Parameter(typeof(IChannelConfiguration));
            ParameterExpression resultVariable = Expression.Variable(typeof(bool));
            var assignments = new List<Expression>
            {
                Expression.Assign(resultVariable, Expression.Constant(true))
            };

            MethodInfo setOptionMethodDefinition = typeof(IChannelConfiguration)
                .FindMembers(MemberTypes.Method, BindingFlags.Instance | BindingFlags.Public, Type.FilterName, "SetOption")
                .Cast<MethodInfo>()
                .First(x => x.IsGenericMethodDefinition);

            foreach (KeyValuePair<ChannelOption, object> p in templateOptions)
            {
                // todo: emit log if verbose is enabled && option is missing
                Type optionType = p.Key.GetType();
                if (!optionType.IsGenericType)
                {
                    throw new InvalidOperationException("Only options of type ChannelOption<T> are supported.");
                }
                if (optionType.GetGenericTypeDefinition() != typeof(ChannelOption<>))
                {
                    throw new NotSupportedException($"Channel option is of an unsupported type `{optionType}`. Only ChannelOption and ChannelOption<T> are supported.");
                }
                Type valueType = optionType.GetGenericArguments()[0];
                MethodInfo setOptionMethod = setOptionMethodDefinition.MakeGenericMethod(valueType);
                assignments.Add(Expression.Assign(
                    resultVariable,
                    Expression.AndAlso(
                        resultVariable,
                        Expression.Call(configParam, setOptionMethod, Expression.Constant(p.Key), Expression.Constant(p.Value, valueType)))));
            }

            return Expression.Lambda<Func<IChannelConfiguration, bool>>(Expression.Block(typeof(bool), new[] { resultVariable }, assignments), configParam).Compile();
        }

        class ServerBootstrapAcceptor : ChannelHandlerAdapter
        {
            readonly IEventLoopGroup childGroup;
            readonly IChannelHandler childHandler;
            readonly Func<IChannelConfiguration, bool> childOptionsSetupFunc;
            // todo: attrs
            //private readonly KeyValuePair<AttributeKey, object>[] childAttrs;

            public ServerBootstrapAcceptor(
                IEventLoopGroup childGroup, IChannelHandler childHandler,
                Func<IChannelConfiguration, bool> childOptionsSetupFunc /*, KeyValuePair<AttributeKey, object>[] childAttrs*/)
            {
                this.childGroup = childGroup;
                this.childHandler = childHandler;
                this.childOptionsSetupFunc = childOptionsSetupFunc;
                //this.childAttrs = childAttrs;
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                var child = (IChannel)msg;

                child.Pipeline.AddLast(this.childHandler);

                if (this.childOptionsSetupFunc != null)
                {
                    if (!this.childOptionsSetupFunc(child.Configuration))
                    {
                        Logger.Warn("Not all configuration options could be set.");
                    }
                }

                // tood: attrs
                //foreach (var e in this.childAttrs) {
                //    child.attr((AttributeKey<Object>) e.getKey()).set(e.getValue());
                //}

                // todo: async/await instead?
                try
                {
                    this.childGroup.GetNext().RegisterAsync(child).ContinueWith(future => ForceClose(child, future.Exception),
                        TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);
                }
                catch (Exception ex)
                {
                    ForceClose(child, ex);
                }
            }

            static void ForceClose(IChannel child, Exception ex)
            {
                child.Unsafe.CloseForcibly();
                Logger.Warn("Failed to register an accepted channel: " + child, ex);
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                IChannelConfiguration config = ctx.Channel.Configuration;
                if (config.AutoRead)
                {
                    // stop accept new connections for 1 second to allow the channel to recover
                    // See https://github.com/netty/netty/issues/1328
                    config.AutoRead = false;
                    ctx.Channel.EventLoop.ScheduleAsync(() => { config.AutoRead = true; }, TimeSpan.FromSeconds(1));
                }
                // still let the ExceptionCaught event flow through the pipeline to give the user
                // a chance to do something with it
                ctx.FireExceptionCaught(cause);
            }
        }

        public override object Clone() => new ServerBootstrap(this);

        public override string ToString()
        {
            var buf = new StringBuilder(base.ToString());
            buf.Length = buf.Length - 1;
            buf.Append(", ");
            if (this.childGroup != null)
            {
                buf.Append("childGroup: ")
                    .Append(this.childGroup.GetType().Name)
                    .Append(", ");
            }
            buf.Append("childOptions: ")
                .Append(this.childOptions.ToDebugString())
                .Append(", ");
            // todo: attrs
            //lock (childAttrs)
            //{
            //    if (!childAttrs.isEmpty())
            //    {
            //        buf.Append("childAttrs: ");
            //        buf.Append(childAttrs);
            //        buf.Append(", ");
            //    }
            //}
            if (this.childHandler != null)
            {
                buf.Append("childHandler: ");
                buf.Append(this.childHandler);
                buf.Append(", ");
            }
            if (buf[buf.Length - 1] == '(')
            {
                buf.Append(')');
            }
            else
            {
                buf[buf.Length - 2] = ')';
                buf.Length = buf.Length - 1;
            }

            return buf.ToString();
        }
    }
}