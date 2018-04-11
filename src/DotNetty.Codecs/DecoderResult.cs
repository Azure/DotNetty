// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Text;
    using DotNetty.Common.Utilities;

    public class DecoderResult
    {
        protected static readonly Signal SignalUnfinished = Signal.ValueOf(typeof(DecoderResult), "UNFINISHED");
        protected static readonly Signal SignalSuccess = Signal.ValueOf(typeof(DecoderResult), "SUCCESS");

        public static readonly DecoderResult Unfinished = new DecoderResult(SignalUnfinished);
        public static readonly DecoderResult Success = new DecoderResult(SignalSuccess);

        public static DecoderResult Failure(Exception cause)
        {
            Contract.Requires(cause != null);
            return new DecoderResult(cause);
        }

        readonly Exception cause;

        protected DecoderResult(Exception cause)
        {
            Contract.Requires(cause != null);
            this.cause = cause;
        }

        public bool IsFinished => !ReferenceEquals(this.cause, SignalUnfinished);

        public bool IsSuccess => ReferenceEquals(this.cause, SignalSuccess);

        public bool IsFailure => !ReferenceEquals(this.cause, SignalSuccess) 
            && !ReferenceEquals(this.cause, SignalUnfinished);

        public Exception Cause => this.IsFailure ? this.cause : null;

        public override string ToString()
        {
            if (!this.IsFinished)
            {
                return "unfinished";
            }

            if (this.IsSuccess)
            {
                return "success";
            }

            string error = this.cause.ToString();
            return new StringBuilder(error.Length + 17)
                .Append("failure(")
                .Append(error)
                .Append(')')
                .ToString();
        }
    }
}
