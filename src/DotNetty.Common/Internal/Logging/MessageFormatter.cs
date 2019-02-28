// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal.Logging
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Formats messages according to very simple substitution rules. Substitutions can be made 1, 2 or more arguments.
    /// <para>For example,</para>
    /// <code>
    /// MessageFormatter.Format(&quot;Hi {}.&quot;, &quot;there&quot;)
    /// </code>
    /// <para>
    /// will return the string "Hi there.".
    /// </para>
    /// <para>
    /// The {} pair is called the <em>formatting anchor</em>. It serves to designate the location where arguments need
    /// to be substituted within the message pattern.
    /// </para>
    /// <para>
    /// In case your message contains the '{' or the '}' character, you do not have to do anything special unless the
    /// '}' character immediately follows '{'. For example,
    /// </para>
    /// <code>
    /// MessageFormatter.Format(&quot;Set {1,2,3} is not equal to {}.&quot;, &quot;1,2&quot;);
    /// </code>
    /// <para>
    /// will return the string "Set {1,2,3} is not equal to 1,2.".
    /// </para>
    /// <para>
    /// If for whatever reason you need to place the string "{}" in the message without its <em>formatting anchor</em>
    /// meaning, then you need to escape the '{' character with '\', that is the backslash character. Only the '{'
    /// character should be escaped. There is no need to escape the '}' character. For example,
    /// </para>
    /// <code>
    /// MessageFormatter.Format(&quot;Set \\{} is not equal to {}.&quot;, &quot;1,2&quot;);
    /// </code>
    /// <para>
    /// will return the string "Set {} is not equal to 1,2.".
    /// </para>
    /// <para>
    /// The escaping behavior just described can be overridden by escaping the escape character '\'. Calling
    /// </para>
    /// <code>
    /// MessageFormatter.Format(&quot;File name is C:\\\\{}.&quot;, &quot;file.zip&quot;);
    /// </code>
    /// <para>
    /// will return the string "File name is C:\file.zip".
    /// </para>
    /// <seealso cref="Format(string, object)" />
    /// <seealso cref="Format(string, object, object)" />
    /// <seealso cref="ArrayFormat(string, object[])" />
    /// </summary>
    public static class MessageFormatter
    {
        static readonly char DELIM_START = '{';
        static readonly string DELIM_STR = "{}";
        static readonly char ESCAPE_CHAR = '\\';

        /// <summary>
        /// Performs single argument substitution for the given <paramref name="messagePattern"/>.
        /// <para>
        /// For example,
        /// </para>
        /// <code>
        /// MessageFormatter.Format(&quot;Hi {}.&quot;, &quot;there&quot;);
        /// </code>
        /// <para>
        /// will return the string "Hi there.".
        /// </para>
        /// </summary>
        /// <param name="messagePattern">The message pattern which will be parsed and formatted</param>
        /// <param name="arg">The argument to be substituted in place of the formatting anchor</param>
        /// <returns>The formatted message</returns>
        public static FormattingTuple Format(string messagePattern, object arg) => ArrayFormat(messagePattern, new[] { arg });

        /// <summary>
        /// Performs a two argument substitution for the given <paramref name="messagePattern"/>.
        /// <para>
        /// For example,
        /// </para>
        /// <code>
        /// MessageFormatter.Format(&quot;Hi {}. My name is {}.&quot;, &quot;Alice&quot;, &quot;Bob&quot;);
        /// </code>
        /// <para>
        /// will return the string "Hi Alice. My name is Bob.".
        /// </para>
        /// </summary>
        /// <param name="messagePattern">The message pattern which will be parsed and formatted</param>
        /// <param name="argA">The argument to be substituted in place of the first formatting anchor</param>
        /// <param name="argB">The argument to be substituted in place of the second formatting anchor</param>
        /// <returns>The formatted message</returns>
        public static FormattingTuple Format(string messagePattern, object argA, object argB) => ArrayFormat(messagePattern, new[] { argA, argB });

        public static Exception GetThrowableCandidate(object[] argArray)
        {
            if (argArray == null || argArray.Length == 0)
            {
                return null;
            }

            return argArray[argArray.Length - 1] as Exception;
        }

        /// <summary>
        /// Same principle as the <see cref="Format(string,object)"/> and <see cref="Format(string,object,object)"/>
        /// methods, except that any number of arguments can be passed in an array.
        /// </summary>
        /// <param name="messagePattern">The message pattern which will be parsed and formatted</param>
        /// <param name="argArray">An array of arguments to be substituted in place of formatting anchors</param>
        /// <returns>The formatted message</returns>
        public static FormattingTuple ArrayFormat(string messagePattern,
            object[] argArray)
        {
            Exception throwableCandidate = GetThrowableCandidate(argArray);

            if (messagePattern == null)
            {
                return new FormattingTuple(null, argArray, throwableCandidate);
            }

            if (argArray == null)
            {
                return new FormattingTuple(messagePattern);
            }

            int i = 0;
            var sbuf = new StringBuilder(messagePattern.Length + 50);

            int l;
            for (l = 0; l < argArray.Length; l++)
            {
                int j = messagePattern.IndexOf(DELIM_STR, i, StringComparison.Ordinal);

                if (j == -1)
                {
                    // no more variables
                    if (i == 0)
                    {
                        // this is a simple string
                        return new FormattingTuple(messagePattern, argArray,
                            throwableCandidate);
                    }
                    else
                    {
                        // add the tail string which contains no variables and return
                        // the result.
                        sbuf.Append(messagePattern.Substring(i, messagePattern.Length - i));
                        return new FormattingTuple(sbuf.ToString(), argArray,
                            throwableCandidate);
                    }
                }
                else
                {
                    if (IsEscapedDelimeter(messagePattern, j))
                    {
                        if (!IsDoubleEscaped(messagePattern, j))
                        {
                            l--; // DELIM_START was escaped, thus should not be incremented
                            sbuf.Append(messagePattern.Substring(i, j - 1 - i));
                            sbuf.Append(DELIM_START);
                            i = j + 1;
                        }
                        else
                        {
                            // The escape character preceding the delimiter start is
                            // itself escaped: "abc x:\\{}"
                            // we have to consume one backward slash
                            sbuf.Append(messagePattern.Substring(i, j - 1 - i));
                            DeeplyAppendParameter(sbuf, argArray[l], new HashSet<object[]>());
                            i = j + 2;
                        }
                    }
                    else
                    {
                        // normal case
                        sbuf.Append(messagePattern.Substring(i, j - i));
                        DeeplyAppendParameter(sbuf, argArray[l], new HashSet<object[]>());
                        i = j + 2;
                    }
                }
            }
            // append the characters following the last {} pair.
            sbuf.Append(messagePattern.Substring(i, messagePattern.Length - i));
            if (l < argArray.Length - 1)
            {
                return new FormattingTuple(sbuf.ToString(), argArray, throwableCandidate);
            }
            else
            {
                return new FormattingTuple(sbuf.ToString(), argArray, null);
            }
        }

        public static bool IsEscapedDelimeter(string messagePattern,
            int delimeterStartIndex)
        {
            if (delimeterStartIndex == 0)
            {
                return false;
            }
            return messagePattern[delimeterStartIndex - 1] == ESCAPE_CHAR;
        }

        public static bool IsDoubleEscaped(string messagePattern, int delimeterStartIndex) => delimeterStartIndex >= 2 && messagePattern[delimeterStartIndex - 2] == ESCAPE_CHAR;

        // special treatment of array values was suggested by 'lizongbo'
        static void DeeplyAppendParameter(StringBuilder sbuf, object o,
            ISet<object[]> seenMap)
        {
            if (o == null)
            {
                sbuf.Append("null");
                return;
            }
            if (!o.GetType().IsArray)
            {
                SafeObjectAppend(sbuf, o);
            }
            else
            {
                // check for primitive array types because they
                // unfortunately cannot be cast to Object[]
                if (o is bool[])
                {
                    BooleanArrayAppend(sbuf, (bool[])o);
                }
                else if (o is byte[])
                {
                    ByteArrayAppend(sbuf, (byte[])o);
                }
                else if (o is char[])
                {
                    CharArrayAppend(sbuf, (char[])o);
                }
                else if (o is short[])
                {
                    ShortArrayAppend(sbuf, (short[])o);
                }
                else if (o is int[])
                {
                    IntArrayAppend(sbuf, (int[])o);
                }
                else if (o is long[])
                {
                    LongArrayAppend(sbuf, (long[])o);
                }
                else if (o is float[])
                {
                    FloatArrayAppend(sbuf, (float[])o);
                }
                else if (o is double[])
                {
                    DoubleArrayAppend(sbuf, (double[])o);
                }
                else
                {
                    ObjectArrayAppend(sbuf, (object[])o, seenMap);
                }
            }
        }

        public static void SafeObjectAppend(StringBuilder sbuf, object o)
        {
            try
            {
                string oAsString = o.ToString();
                sbuf.Append(oAsString);
            }
            catch (Exception t)
            {
                Console.Error.WriteLine("Failed ToString() invocation on an object of type ["
                    + o.GetType().Name + "]:" + Environment.NewLine + t);
                sbuf.Append("[FAILED toString()]");
            }
        }

        static void ObjectArrayAppend(StringBuilder sbuf, object[] a,
            ISet<object[]> seenMap)
        {
            sbuf.Append('[');
            if (!seenMap.Contains(a))
            {
                seenMap.Add(a);
                int len = a.Length;
                for (int i = 0; i < len; i++)
                {
                    DeeplyAppendParameter(sbuf, a[i], seenMap);
                    if (i != len - 1)
                    {
                        sbuf.Append(", ");
                    }
                }
                // allow repeats in siblings
                seenMap.Remove(a);
            }
            else
            {
                sbuf.Append("...");
            }
            sbuf.Append(']');
        }

        static void BooleanArrayAppend(StringBuilder sbuf, bool[] a)
        {
            sbuf.Append('[');
            int len = a.Length;
            for (int i = 0; i < len; i++)
            {
                sbuf.Append(a[i]);
                if (i != len - 1)
                {
                    sbuf.Append(", ");
                }
            }
            sbuf.Append(']');
        }

        static void ByteArrayAppend(StringBuilder sbuf, byte[] a)
        {
            sbuf.Append('[');
            int len = a.Length;
            for (int i = 0; i < len; i++)
            {
                sbuf.Append(a[i]);
                if (i != len - 1)
                {
                    sbuf.Append(", ");
                }
            }
            sbuf.Append(']');
        }

        static void CharArrayAppend(StringBuilder sbuf, char[] a)
        {
            sbuf.Append('[');
            int len = a.Length;
            for (int i = 0; i < len; i++)
            {
                sbuf.Append(a[i]);
                if (i != len - 1)
                {
                    sbuf.Append(", ");
                }
            }
            sbuf.Append(']');
        }

        static void ShortArrayAppend(StringBuilder sbuf, short[] a)
        {
            sbuf.Append('[');
            int len = a.Length;
            for (int i = 0; i < len; i++)
            {
                sbuf.Append(a[i]);
                if (i != len - 1)
                {
                    sbuf.Append(", ");
                }
            }
            sbuf.Append(']');
        }

        static void IntArrayAppend(StringBuilder sbuf, int[] a)
        {
            sbuf.Append('[');
            int len = a.Length;
            for (int i = 0; i < len; i++)
            {
                sbuf.Append(a[i]);
                if (i != len - 1)
                {
                    sbuf.Append(", ");
                }
            }
            sbuf.Append(']');
        }

        static void LongArrayAppend(StringBuilder sbuf, long[] a)
        {
            sbuf.Append('[');
            int len = a.Length;
            for (int i = 0; i < len; i++)
            {
                sbuf.Append(a[i]);
                if (i != len - 1)
                {
                    sbuf.Append(", ");
                }
            }
            sbuf.Append(']');
        }

        static void FloatArrayAppend(StringBuilder sbuf, float[] a)
        {
            sbuf.Append('[');
            int len = a.Length;
            for (int i = 0; i < len; i++)
            {
                sbuf.Append(a[i]);
                if (i != len - 1)
                {
                    sbuf.Append(", ");
                }
            }
            sbuf.Append(']');
        }

        static void DoubleArrayAppend(StringBuilder sbuf, double[] a)
        {
            sbuf.Append('[');
            int len = a.Length;
            for (int i = 0; i < len; i++)
            {
                sbuf.Append(a[i]);
                if (i != len - 1)
                {
                    sbuf.Append(", ");
                }
            }
            sbuf.Append(']');
        }
    }
}