using System;
using System.Configuration;

namespace Echo.Client
{
    using System.Net;

    public static class EchoClientSettings
    {
        public static bool IsSsl
        {
            get
            {
                string ssl = ConfigurationManager.AppSettings["ssl"];
                return !string.IsNullOrEmpty(ssl) && bool.Parse(ssl);
            }
        }

        public static IPAddress Host
        {
            get { return IPAddress.Parse(ConfigurationManager.AppSettings["host"]); }
        }

        public static int Port
        {
            get { return int.Parse(ConfigurationManager.AppSettings["port"]); }
        }

        public static int Size
        {
            get { return int.Parse(ConfigurationManager.AppSettings["size"]); }
        }
    }
}
