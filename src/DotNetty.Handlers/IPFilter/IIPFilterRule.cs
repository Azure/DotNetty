// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace DotNetty.Handlers.IPFilter
{
    using System.Net;

    /// <summary>
    /// Implement this interface to create new rules.
    /// </summary>
    public interface IIPFilterRule
    {
        /// <summary>
        ///  This method should return true if remoteAddress is valid according to your criteria. False otherwise.
        /// </summary>
        bool Matches(IPEndPoint remoteAddress);
        
        /// <summary>
        /// This method should return <see cref="IPFilterRuleType.Accept"/> if all
        /// <see cref="Matches"/> for which <see cref="Matches"/>
        /// returns true should the accepted. If you want to exclude all of those IP addresses then
        /// <see cref="IPFilterRuleType.Reject"/> should be returned.
        /// </summary>
        IPFilterRuleType RuleType { get; }
    }
}