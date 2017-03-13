// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System.Net;
    using DotNetty.Common;

    public interface IAddressedEnvelope<out T> : IReferenceCounted
    {
        T Content { get; }

        EndPoint Sender { get; }

        EndPoint Recipient { get; }
    }
}