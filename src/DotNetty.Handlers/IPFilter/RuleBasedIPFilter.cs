// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace DotNetty.Handlers.IPFilter
{
    using System;
    using System.Net;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// This class allows one to filter new <see cref="IChannel"/>s based on the
    /// <see cref="IIPFilterRule"/>s passed to its constructor. If no rules are provided, all connections
    /// will be accepted.
    ///
    /// If you would like to explicitly take action on rejected <see cref="IChannel"/>s, you should override
    /// <see cref="RuleBasedIPFilter.ChannelRejected"/>.
    /// </summary>
    public class RuleBasedIPFilter : AbstractRemoteAddressFilter<IPEndPoint>
    {
        readonly IIPFilterRule[] rules;

        public RuleBasedIPFilter(params IIPFilterRule[] rules)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        protected override bool Accept(IChannelHandlerContext ctx, IPEndPoint remoteAddress)
        {
            foreach (IIPFilterRule rule in this.rules) {
                if (rule == null) {
                    break;
                }
                if (rule.Matches(remoteAddress)) {
                    return rule.RuleType == IPFilterRuleType.Accept;
                }
            }
            return true;
        }
    }
}