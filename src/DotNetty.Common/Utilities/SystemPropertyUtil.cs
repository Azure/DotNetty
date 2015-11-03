using System;

namespace DotNetty.Common.Utilities
{
    using System.Diagnostics.Contracts;

    /// <summary>
    /// A collection of utility methods to retrieve and parse the values of the .Net Environment properties.
    /// </summary>
    public static class SystemPropertyUtil
    {
        public static bool Contains(string key)
        {
            return Get(key) != null;
        }

        public static string Get(string key)
        {
            return Get(key, null);
        }

        public static string Get(string key, string def)
        {
            Contract.Requires(string.IsNullOrWhiteSpace(key));
            string value = null;
            try
            {
                value = Environment.GetEnvironmentVariable(key);
            }
            catch
            {
                // ignored
            }

            if (value == null)
            {
                return def;
            }
            return value;
        }

        public static int GetInt(string key, int def)
        {
            string value = Get(key);
            if (value == null)
            {
                return def;
            }
            value = value.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(value))
            {
                return def;
            }
            int val;
            if (int.TryParse(value, out val))
            {
                return val;
            }
            return def;
        }

        public static long GetLong(string key, long def)
        {
            string value = Get(key);
            if (value == null)
            {
                return def;
            }
            value = value.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(value))
            {
                return def;
            }
            long val;
            if (long.TryParse(value, out val))
            {
                return val;
            }
            return def;
        }

        public static bool GetBoolean(string key, bool def)
        {
            string value = Get(key);
            if (value == null)
            {
                return def;
            }
            value = value.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }
            if ("true".Equals(value, StringComparison.OrdinalIgnoreCase) || "yes".Equals(value, StringComparison.OrdinalIgnoreCase) || "1".Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if ("false".Equals(value, StringComparison.OrdinalIgnoreCase) || "no".Equals(value, StringComparison.OrdinalIgnoreCase) || "0".Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return def;
        }
    }
}