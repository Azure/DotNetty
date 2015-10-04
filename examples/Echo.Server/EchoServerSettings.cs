// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Echo.Server
{
    using System.Configuration;

    public static class EchoServerSettings
    {
        public static bool IsSsl
        {
            get
            {
                string ssl = ConfigurationManager.AppSettings["ssl"];
                return !string.IsNullOrEmpty(ssl) && bool.Parse(ssl);
            }
        }

        public static int Port
        {
            get { return int.Parse(ConfigurationManager.AppSettings["port"]); }
        }
    }
}