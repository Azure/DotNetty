// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Common.Utilities;

    public static class HeadersUtils
    {
        public static List<string> GetAllAsString<TKey, TValue>(IHeaders<TKey, TValue> headers, TKey name)
            where TKey : class
        {
            IList<TValue> allNames = headers.GetAll(name);
            var values = new List<string>();

            // ReSharper disable once ForCanBeConvertedToForeach
            // Avoid enumerator allocation
            for (int i = 0; i < allNames.Count; i++)
            {
                TValue value = allNames[i];
                values.Add(value?.ToString());
            }

            return values;
        }

        public static bool TryGetAsString<TKey, TValue>(IHeaders<TKey, TValue> headers, TKey name, out string value)
            where TKey : class
        {
            if (headers.TryGet(name, out TValue orig))
            {
                value = orig.ToString();
                return true;
            }
            else
            {
                value = default(string);
                return false;
            }
        }

        public static string ToString<TKey, TValue>(IEnumerable<HeaderEntry<TKey, TValue>> headers, int size)
            where TKey : class
        {
            string simpleName = StringUtil.SimpleClassName(headers);
            if (size == 0)
            {
                return simpleName + "[]";
            }
            else
            {
                // original capacity assumes 20 chars per headers
                StringBuilder sb = new StringBuilder(simpleName.Length + 2 + size * 20)
                    .Append(simpleName)
                    .Append('[');
                foreach (HeaderEntry<TKey, TValue> header in headers)
                {
                    sb.Append(header.Key).Append(": ").Append(header.Value).Append(", ");
                }
                sb.Length = sb.Length - 2;
                return sb.Append(']').ToString();
            }
        }

        public static IList<string> NamesAsString(IHeaders<ICharSequence, ICharSequence> headers)
        {
            ISet<ICharSequence> allNames = headers.Names();

            var names = new List<string>();

            foreach (ICharSequence name in allNames)
            {
                names.Add(name.ToString());
            }

            return names;
        }

        internal static void ThrowArgumentNullException(string name) => throw new ArgumentNullException(name);

        internal static void ThrowArgumentException(string message) => throw new ArgumentException(message);

        internal static void ThrowInvalidOperationException(string message) => throw new InvalidOperationException(message);
    }
}
