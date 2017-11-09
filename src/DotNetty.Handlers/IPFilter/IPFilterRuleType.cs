// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.IPFilter
{
    /// <summary>
    /// Used in <see cref="IIPFilterRule"/> to decide if a matching IP Address should be allowed or denied to connect.
    /// </summary>
    public enum IPFilterRuleType
    {
        Accept, 
        Reject
    }
}