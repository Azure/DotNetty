﻿using System;

namespace DotNetty.Transport.Channels.Groups
{
    public static class ChannelMatchers
    {
        static readonly IChannelMatcher AllMatcher = new AllChannelMatcher();
        static readonly IChannelMatcher ServerChannelMatcher = IsInstanceOf(typeof(IServerChannel));
        static readonly IChannelMatcher NonServerChannelMatcher = IsNotInstanceOf(typeof(IServerChannel));

        public static IChannelMatcher IsServerChannel()
        {
            return ServerChannelMatcher;
        }

        public static IChannelMatcher IsNonServerChannel()
        {
            return NonServerChannelMatcher;
        }

        public static IChannelMatcher All()
        {
            return AllMatcher;
        }

        public static IChannelMatcher IsNot(IChannel channel)
        {
            return Invert(Is(channel));
        }

        public static IChannelMatcher Is(IChannel channel)
        {
            return new InstanceMatcher(channel);
        }

        public static IChannelMatcher IsInstanceOf(Type type)
        {
            return new TypeMatcher(type);
        }

        public static IChannelMatcher IsNotInstanceOf(Type type)
        {
            return Invert(IsInstanceOf(type));
        }

        public static IChannelMatcher Invert(IChannelMatcher matcher)
        {
            return new InvertMatcher(matcher);
        }

        public static IChannelMatcher Compose(params IChannelMatcher[] matchers)
        {
            if (matchers.Length < 1)
            {
                throw new ArgumentOutOfRangeException("matchers");
            }
            if (matchers.Length == 1)
            {
                return matchers[0];
            }
            return new CompositeMatcher(matchers);
        }

        sealed class AllChannelMatcher : IChannelMatcher
        {
            public bool Matches(IChannel channel)
            {
                return true;
            }
        }

        sealed class CompositeMatcher : IChannelMatcher
        {
            readonly IChannelMatcher[] matchers;

            public CompositeMatcher(params IChannelMatcher[] matchers)
            {
                this.matchers = matchers;
            }

            public bool Matches(IChannel channel)
            {
                foreach (IChannelMatcher m in this.matchers)
                {
                    if (!m.Matches(channel))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        sealed class InvertMatcher : IChannelMatcher
        {
            readonly IChannelMatcher matcher;

            public InvertMatcher(IChannelMatcher matcher)
            {
                this.matcher = matcher;
            }

            public bool Matches(IChannel channel)
            {
                return !this.matcher.Matches(channel);
            }
        }

        sealed class InstanceMatcher : IChannelMatcher
        {
            readonly IChannel channel;

            public InstanceMatcher(IChannel channel)
            {
                this.channel = channel;
            }

            public bool Matches(IChannel ch)
            {
                return this.channel == ch;
            }
        }

        sealed class TypeMatcher : IChannelMatcher
        {
            readonly Type type;

            public TypeMatcher(Type clazz)
            {
                this.type = clazz;
            }

            public bool Matches(IChannel channel)
            {
                return this.type.IsInstanceOfType(channel);
            }
        }
    }
}