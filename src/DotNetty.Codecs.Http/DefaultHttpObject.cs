// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System.Diagnostics.Contracts;

    public class DefaultHttpObject : IHttpObject
    {
        const int HashCodePrime = 31;
        DecoderResult decoderResult = DecoderResult.Success;

        protected DefaultHttpObject()
        {
        }

        public DecoderResult Result
        {
            get => this.decoderResult;
            set
            {
                Contract.Requires(value != null);
                this.decoderResult = value;
            }
        }

        public override int GetHashCode()
        {
            int result = 1;
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            result = HashCodePrime * result + this.decoderResult.GetHashCode();
            return result;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is DefaultHttpObject other))
            {
                return false;
            }
            return this.decoderResult.Equals(other.decoderResult);
        }
    }
}
