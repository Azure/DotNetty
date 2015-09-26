using System;

namespace DotNetty.Common.Utilities
{
    /// <summary>
    /// A collection of utility methods to retrieve and parse the values of the .Net Environment properties.
    /// </summary>
    public class SystemPropertyUtil
    {
        private SystemPropertyUtil() { }
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
            if (key == null)
                throw new ArgumentNullException("key");
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("key must be non empty");
            string value = null;
            try
            {
                value = Environment.GetEnvironmentVariable(key);
            }
            catch { }

            if (value == null)
                return def;
            return value;
        }


        public static int GetInt(string key, int def)
        {
            var value = Get(key);
            if (value == null)
                return def;
            value = value.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(value))
                return def;
            int val;
            if (int.TryParse(value, out val))
                return val;
            return def;

        }

        public static long GetLong(string key, long def)
        {
            var value = Get(key);
            if (value == null)
                return def;
            value = value.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(value))
                return def;
            long val;
            if (long.TryParse(value, out val))
                return val;
            return def;
        }

        public static bool GetBoolean(string key, bool def)
        {
            var value = Get(key);
            if (value == null)
                return def;
            value = value.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(value))
                return true;
            if ("true".Equals(value) || "yes".Equals(value) || "1".Equals(value))
                return true;
            if ("false".Equals(value) || "no".Equals(value) || "0".Equals(value))
                return false;
            return def;
        }
    }
}
