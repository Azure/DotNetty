using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNetty.Transport.Channels.Groups
{
    public sealed class ChannelMatchers
    {

        private static readonly IChannelMatcher AllMatchers = new AllMatcher();
        private static readonly IChannelMatcher ServerChannelMatcher = IsInstanceOf(typeof(IServerChannel));
        private static readonly IChannelMatcher NonServerChannelMatcher = IsNotInstanceOf(typeof(IServerChannel));

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
            return AllMatchers;
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
                throw new ArgumentOutOfRangeException("matchers at least contain one elemnt");
            if (matchers.Length == 1)
                return matchers[0];
            return new CompositeMatcher(matchers);
        }

        private sealed class AllMatcher : IChannelMatcher
        {

            public bool Matches(IChannel channel)
            {
                return true;
            }
        }

        private sealed class CompositeMatcher : IChannelMatcher
        {
            private readonly IChannelMatcher[] matchers;

            public CompositeMatcher(params IChannelMatcher[] matchers)
            {
                this.matchers = matchers;
            }



            public bool Matches(IChannel channel)
            {
                foreach(var m in matchers)
                {
                    if (!m.Matches(channel))
                        return false;
                }
                return true;
            }
        }


        private sealed class InvertMatcher : IChannelMatcher
        {
            private readonly IChannelMatcher matcher;

            public InvertMatcher(IChannelMatcher matcher)
            {
                this.matcher = matcher;
            }



            public bool Matches(IChannel channel)
            {
                return !matcher.Matches(channel);
            }
        }

        private sealed class InstanceMatcher : IChannelMatcher
        {
            private readonly IChannel channel;

            public InstanceMatcher(IChannel channel)
            {
                this.channel = channel;
            }



            public bool Matches(IChannel channel)
            {
                return this.channel == channel;
            }
        }

        private sealed class TypeMatcher: IChannelMatcher
        {
            private readonly Type type;

            public TypeMatcher(Type clazz)
            {
                this.type = clazz;
            }

            public bool Matches(IChannel channel)
            {
                return this.type.IsInstanceOfType(channel.GetType());
            }
        }

    }
}
