// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal.Logging
{
    using System;
    using System.Diagnostics.Contracts;

    /// <summary>
    ///     Holds the results of formatting done by {@link MessageFormatter}.
    /// </summary>
    class FormattingTuple
    {
        static readonly FormattingTuple NULL = new FormattingTuple(null);

        public FormattingTuple(string message)
            : this(message, null, null)
        {
        }

        public FormattingTuple(string message, object[] argArray, Exception exception)
        {
            this.Message = message;
            this.Exception = exception;
            if (exception == null)
            {
                this.ArgArray = argArray;
            }
            else
            {
                this.ArgArray = GetTrimmedCopy(argArray);
            }
        }

        static object[] GetTrimmedCopy(object[] argArray)
        {
            Contract.Requires(argArray != null && argArray.Length > 0);

            int trimemdLen = argArray.Length - 1;
            var trimmed = new object[trimemdLen];
            Array.Copy(argArray, 0, trimmed, 0, trimemdLen);
            return trimmed;
        }

        public string Message { get; private set; }

        public object[] ArgArray { get; private set; }

        public Exception Exception { get; private set; }
    }
}