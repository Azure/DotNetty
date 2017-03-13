// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using System.Text;

    /// <summary>
    ///     Thrown if an unsupported message is received by an codec.
    /// </summary>
    public class UnsupportedMessageTypeException : CodecException
    {
        public UnsupportedMessageTypeException(object message, params Type[] expectedTypes)
            : base(ComposeMessage(message?.GetType().Name ?? "null", expectedTypes))
        {
        }

        public UnsupportedMessageTypeException()
        {
        }

        public UnsupportedMessageTypeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public UnsupportedMessageTypeException(string message)
            : base(message)
        {
        }

        public UnsupportedMessageTypeException(Exception innerException)
            : base(innerException)
        {
        }

        static string ComposeMessage(string actualType, params Type[] expectedTypes)
        {
            var buf = new StringBuilder(actualType);

            if (expectedTypes != null && expectedTypes.Length > 0)
            {
                buf.Append(" (expected: ").Append(expectedTypes[0].Name);
                for (int i = 1; i < expectedTypes.Length; i++)
                {
                    Type t = expectedTypes[i];
                    if (t == null)
                    {
                        break;
                    }
                    buf.Append(", ").Append(t.Name);
                }
                buf.Append(')');
            }

            return buf.ToString();
        }
    }
}