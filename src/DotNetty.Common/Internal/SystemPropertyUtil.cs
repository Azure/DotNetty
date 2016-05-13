// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    using System;
    using System.Diagnostics.Contracts;
    using DotNetty.Common.Internal.Logging;

    /// <summary>
    ///     A collection of utility methods to retrieve and parse the values of the system properties (Environment variables).
    /// </summary>
    public static class SystemPropertyUtil
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance(typeof(SystemPropertyUtil));
        static bool loggedException;

        /// <summary>
        ///     Returns <c>true</c> if and only if the system property with the specified <c>key</c>
        ///     exists.
        /// </summary>
        public static bool Contains(string key) => Get(key) != null;

        /// <summary>
        ///     Returns the value of the system property with the specified
        ///     <c>key</c>, while falling back to <c>null</c> if the property access fails.
        /// </summary>
        /// <returns>the property value or <c>null</c></returns>
        public static string Get(string key) => Get(key, null);

        /// <summary>
        ///     Returns the value of the system property with the specified
        ///     <c>key</c>, while falling back to the specified default value if
        ///     the property access fails.
        /// </summary>
        /// <returns>
        ///     the property value.
        ///     <c>def</c> if there's no such property or if an access to the
        ///     specified property is not allowed.
        /// </returns>
        public static string Get(string key, string def)
        {
            Contract.Requires(!string.IsNullOrEmpty(key));

            try
            {
                return Environment.GetEnvironmentVariable(key) ?? def;
            }
            catch (Exception e)
            {
                if (!loggedException)
                {
                    Log("Unable to retrieve a system property '" + key + "'; default values will be used.", e);
                    loggedException = true;
                }
                return def;
            }
        }

        /// <summary>
        ///     Returns the value of the system property with the specified
        ///     <c>key</c>, while falling back to the specified default value if
        ///     the property access fails.
        /// </summary>
        /// <returns>
        ///     the property value or <c>def</c> if there's no such property or
        ///     if an access to the specified property is not allowed.
        /// </returns>
        public static bool GetBoolean(string key, bool def)
        {
            string value = Get(key);
            if (value == null)
            {
                return def;
            }

            value = value.Trim().ToLowerInvariant();
            if (value.Length == 0)
            {
                return true;
            }

            if ("true".Equals(value, StringComparison.OrdinalIgnoreCase)
                || "yes".Equals(value, StringComparison.OrdinalIgnoreCase)
                || "1".Equals(value, StringComparison.Ordinal))
            {
                return true;
            }

            if ("false".Equals(value, StringComparison.OrdinalIgnoreCase)
                || "no".Equals(value, StringComparison.OrdinalIgnoreCase)
                || "0".Equals(value, StringComparison.Ordinal))
            {
                return false;
            }

            Log(
                "Unable to parse the boolean system property '" + key + "':" + value + " - " +
                    "using the default value: " + def);

            return def;
        }

        /// <summary>
        ///     Returns the value of the system property with the specified
        ///     <c>key</c>, while falling back to the specified default value if
        ///     the property access fails.
        /// </summary>
        /// <returns>
        ///     the property value.
        ///     <c>def</c> if there's no such property or if an access to the
        ///     specified property is not allowed.
        /// </returns>
        public static int GetInt(string key, int def)
        {
            string value = Get(key);
            if (value == null)
            {
                return def;
            }

            value = value.Trim().ToLowerInvariant();
            int result;
            if (!int.TryParse(value, out result))
            {
                result = def;

                Log(
                    "Unable to parse the integer system property '" + key + "':" + value + " - " +
                        "using the default value: " + def);
            }
            return result;
        }

        /// <summary>
        ///     Returns the value of the system property with the specified
        ///     <c>key</c>, while falling back to the specified default value if
        ///     the property access fails.
        /// </summary>
        /// <returns>
        ///     the property value.
        ///     <c>def</c> if there's no such property or if an access to the
        ///     specified property is not allowed.
        /// </returns>
        public static long GetLong(string key, long def)
        {
            string value = Get(key);
            if (value == null)
            {
                return def;
            }

            long result;
            if (!long.TryParse(value, out result))
            {
                result = def;
                Log(
                    "Unable to parse the long integer system property '" + key + "':" + value + " - " +
                        "using the default value: " + def);
            }
            return result;
        }

        static void Log(string msg) => Logger.Warn(msg);

        static void Log(string msg, Exception e) => Logger.Warn(msg, e);
    }
}