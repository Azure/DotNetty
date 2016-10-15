// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Echo.Server
{
    using System.Configuration;

    public static class EchoServerSettings
    {
        public static bool IsSsl
        {
            get { return (ConfigurationManager.AppSettings["ssl"] ?? string.Empty).ToLower() == "true"; }
        }

        public static int Port => int.Parse(ConfigurationManager.AppSettings["port"]);
    }
}
