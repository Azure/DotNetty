// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Http.WebSockets.Extensions
{
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;

    public sealed class WebSocketExtensionData
    {
        readonly string name;
        readonly Dictionary<string, string> parameters;

        public WebSocketExtensionData(string name, IDictionary<string, string> parameters)
        {
            Contract.Requires(name != null);
            Contract.Requires(parameters != null);

            this.name = name;
            this.parameters = new Dictionary<string, string>(parameters);
        }

        public string Name => this.name;

        public Dictionary<string, string> Parameters => this.parameters;
    }
}
