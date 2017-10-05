// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    public interface IHttpMessage : IHttpObject
    {
        HttpVersion ProtocolVersion { get; }

        IHttpMessage SetProtocolVersion(HttpVersion version);

        HttpHeaders Headers { get; }
    }
}
