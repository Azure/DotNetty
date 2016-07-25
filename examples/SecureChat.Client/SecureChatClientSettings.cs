// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SecureChat.Client
{
    using System.Configuration;
    using System.Net;

    public static class SecureChatClientSettings
    {
        public static bool IsSsl
        {
            get
            {
                string ssl = System.Configuration.ConfigurationManager.AppSettings["ssl"];
                return !string.IsNullOrEmpty(ssl) && bool.Parse(ssl);
            }
        }

        public static IPAddress Host => IPAddress.Parse(ConfigurationManager.AppSettings["host"]);
        public static int Port => int.Parse(ConfigurationManager.AppSettings["port"]);
        public static int Count => int.Parse(ConfigurationManager.AppSettings["count"]);
    }
}