using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DotNetty.Common.Utilities
{
    /// <summary>
    /// String utility class.
    /// </summary>
    public class StringUtil
    {
        public static readonly string EMPTY_STRING = "";
        public static readonly string NEWLINE;

        public const char DOUBLE_QUOTE = '\"';
        public const char COMMA = ',';
        public const char LINE_FEED = '\n';
        public const char CARRIAGE_RETURN = '\r';
        public const char TAB = '\t';

        public const byte UPPER_CASE_TO_LOWER_CASE_ASCII_OFFSET = 'a' - 'A';
        private static readonly string[] BYTE2HEX_PAD = new String[256];
        private static readonly string[] BYTE2HEX_NOPAD = new String[256];

        /**
         * 2 - Quote character at beginning and end.
         * 5 - Extra allowance for anticipated escape characters that may be added.
        */
        private static readonly int CSV_NUMBER_ESCAPE_CHARACTERS = 2 + 5;
        private static readonly char PACKAGE_SEPARATOR_CHAR = '.';

        private StringUtil()
        {

        }

        static StringUtil()
        {
            // Determine the newline character of the current platform.
            string newLine;
            try
            {
                //newLine = new Formatter().format("%n").toString();
                newLine = Environment.NewLine;
            }
            catch
            {
                // Should not reach here, but just in case.
                newLine = "\n";
            }

            NEWLINE = newLine;

            // Generate the lookup table that converts a byte into a 2-digit hexadecimal integer.
            int i;
            for (i = 0; i < 10; i++)
            {
                StringBuilder buf = new StringBuilder(2);
                buf.Append('0');
                buf.Append(i);
                BYTE2HEX_PAD[i] = buf.ToString();
                BYTE2HEX_NOPAD[i] = (i).ToString();
            }
            for (; i < 16; i++)
            {
                StringBuilder buf = new StringBuilder(2);
                char c = (char)('a' + i - 10);
                buf.Append('0');
                buf.Append(c);
                BYTE2HEX_PAD[i] = buf.ToString();
                BYTE2HEX_NOPAD[i] = c.ToString();/* String.valueOf(c);*/
            }
            for (; i < BYTE2HEX_PAD.Length; i++)
            {
                StringBuilder buf = new StringBuilder(2);
                buf.Append(i.ToString("X")/*Integer.toHexString(i)*/);
                String str = buf.ToString();
                BYTE2HEX_PAD[i] = str;
                BYTE2HEX_NOPAD[i] = str;
            }
        }


        /// <summary>
        /// Splits the specified {@link String} with the specified delimiter.  This operation is a simplified and optimized
        /// version of {@link String#split(String)}.
        /// </summary>              
        public static string[] Split(string value, char delim)
        {
            int end = value.Length;
            List<string> res = new List<string>();

            int start = 0;
            for (int i = 0; i < end; i++)
            {
                if (value[i] == delim)
                {
                    if (start == i)
                    {
                        res.Add(EMPTY_STRING);
                    }
                    else
                    {
                        res.Add(value.Substring(start, i));
                    }
                    start = i + 1;
                }
            }

            if (start == 0)
            { // If no delimiter was found in the value
                res.Add(value);
            }
            else
            {
                if (start != end)
                {
                    // Add the last element if it's not empty.
                    res.Add(value.Substring(start, end));
                }
                else
                {
                    // Truncate trailing empty elements.
                    for (int i = res.Count - 1; i >= 0; i--)
                    {
                        if (res[i] == "")
                        {
                            res.Remove(res[i]);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            return res.ToArray();
        }

        /// <summary>
        /// Splits the specified {@link String} with the specified delimiter in maxParts maximum parts.
        /// This operation is a simplified and optimized
        /// version of {@link String#split(String, int)}.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="delim"></param>
        /// <param name="maxParts"></param>
        /// <returns></returns>
        public static string[] Split(string value, char delim, int maxParts)
        {
            int end = value.Length;
            List<string> res = new List<string>();

            int start = 0;
            int cpt = 1;
            for (int i = 0; i < end && cpt < maxParts; i++)
            {
                if (value[i] == delim)
                {
                    if (start == i)
                    {
                        res.Add(EMPTY_STRING);
                    }
                    else
                    {
                        res.Add(value.Substring(start, i));
                    }
                    start = i + 1;
                    cpt++;
                }
            }

            if (start == 0)
            { // If no delimiter was found in the value
                res.Add(value);
            }
            else
            {
                if (start != end)
                {
                    // Add the last element if it's not empty.
                    res.Add(value.Substring(start, end));
                }
                else
                {
                    // Truncate trailing empty elements.
                    for (int i = res.Count - 1; i >= 0; i--)
                    {
                        if (res[i] == "")
                        {
                            res.Remove(res[i]);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            return res.ToArray();
        }

        /// <summary>
        /// Get the item after one char delim if the delim is found (else null).
        /// This operation is a simplified and optimized
        /// version of {@link String#split(String, int)}.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="delim"></param>
        /// <returns></returns>
        public static string SubstringAfter(String value, char delim)
        {
            int pos = value.IndexOf(delim);
            if (pos >= 0)
            {
                return value.Substring(pos + 1);
            }
            return null;
        }

        /// <summary>
        ///  Converts the specified byte value into a 2-digit hexadecimal integer.
        /// </summary>
        public static string ByteToHexStringPadded(int value)
        {
            return BYTE2HEX_PAD[value & 0xff];
        }

        //    /**
        // * Converts the specified byte value into a 2-digit hexadecimal integer and appends it to the specified buffer.
        // */
        //public static <T extends Appendable> T byteToHexStringPadded(T buf, int value) {
        //    try {
        //        buf.append(byteToHexStringPadded(value));
        //    } catch (IOException e) {
        //        PlatformDependent.throwException(e);
        //    }
        //    return buf;
        //}


        /// <summary>
        /// Converts the specified byte array into a hexadecimal value.
        /// </summary>
        public static string ToHexStringPadded(byte[] src)
        {
            return ToHexStringPadded(src, 0, src.Length);
        }

        /// <summary>
        /// Converts the specified byte array into a hexadecimal value.
        /// </summary>
        public static string ToHexStringPadded(byte[] src, int offset, int length)
        {
            int end = offset + length;
            StringBuilder sb = new StringBuilder(length << 1);
            for (int i = offset; i < end; i++)
                sb.Append(ByteToHexStringPadded(src[i]));
            return sb.ToString();

        }

        public static StringBuilder ToHexStringPadded(StringBuilder sb, byte[] src, int offset, int length)
        {
            int end = offset + length;
            for (int i = offset; i < end; i++)
                sb.Append(ByteToHexStringPadded(src[i]));
            return sb;
        }

        /// <summary>
        /// Converts the specified byte value into a hexadecimal integer.
        /// </summary>
        public static string ByteToHexString(int value)
        {
            return BYTE2HEX_NOPAD[value & 0xff];
        }

        public static StringBuilder ByteToHexString(StringBuilder buf, int value)
        {
            return buf.Append(ByteToHexString(value));

        }

        public static string ToHexString(byte[] src)
        {
            return ToHexString(src, 0, src.Length);
        }

        public static string ToHexString(byte[] src, int offset, int length)
        {
            return ToHexString(new StringBuilder(length << 1), src, offset, length).ToString();
        }

        public static StringBuilder ToHexString(StringBuilder dst, byte[] src)
        {
            return ToHexString(dst, src, 0, src.Length);
        }

        /// <summary>
        /// Converts the specified byte array into a hexadecimal value and appends it to the specified buffer.
        /// </summary>
        public static StringBuilder ToHexString(StringBuilder dst, byte[] src, int offset, int length)
        {
            Debug.Assert(length >= 0);
            if (length == 0)
                return dst;
            int end = offset + length;
            int endMinusOne = end - 1;
            int i;
            // Skip preceding zeroes.
            for (i = offset; i < endMinusOne; i++)
            {
                if (src[i] != 0)
                {
                    break;
                }
            }

            ByteToHexString(dst, src[i++]);
            int remaining = end - i;
            ToHexStringPadded(dst, src, i, remaining);

            return dst;
        }

        /// <summary>
        /// Escapes the specified value, if necessary according to
        /// <a href="https://tools.ietf.org/html/rfc4180#section-2">RFC-4180</a>.
        /// </summary>
        /// <param name="value">The value which will be escaped according to
        /// <a href="https://tools.ietf.org/html/rfc4180#section-2">RFC-4180</a></param>
        /// <returns>the escaped value if necessary, or the value unchanged</returns>
        public static string EscapeCsv(string value)
        {
            int length = value.Length;
            if (length == 0)
            {
                return value;
            }
            int last = length - 1;
            bool quoted = IsDoubleQuote(value[0]) && IsDoubleQuote(value[last]) && length != 1;
            bool foundSpecialCharacter = false;
            bool escapedDoubleQuote = false;
            StringBuilder escaped = new StringBuilder(length + CSV_NUMBER_ESCAPE_CHARACTERS).Append(DOUBLE_QUOTE);
            for (int i = 0; i < length; i++)
            {
                char current = value[i];
                switch (current)
                {
                    case DOUBLE_QUOTE:
                        if (i == 0 || i == last)
                        {
                            if (!quoted)
                            {
                                escaped.Append(DOUBLE_QUOTE);
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else
                        {
                            bool isNextCharDoubleQuote = IsDoubleQuote(value[i + 1]);
                            if (!IsDoubleQuote(value[i - 1]) &&
                                    (!isNextCharDoubleQuote || i + 1 == last))
                            {
                                escaped.Append(DOUBLE_QUOTE);
                                escapedDoubleQuote = true;
                            }
                            break;
                        }
                        break;
                    case LINE_FEED:
                    case CARRIAGE_RETURN:
                    case COMMA:
                        foundSpecialCharacter = true;
                        break;
                }
                escaped.Append(current);
            }
            return escapedDoubleQuote || foundSpecialCharacter && !quoted ?
                    escaped.Append(DOUBLE_QUOTE).ToString() : value;
        }

        private static bool IsDoubleQuote(char c)
        {
            return c == DOUBLE_QUOTE;
        }
    }
}
