// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//JZlib 0.0.* were released under the GNU LGPL license.Later, we have switched
//over to a BSD-style license. 

//------------------------------------------------------------------------------
//Copyright (c) 2000-2011 ymnk, JCraft, Inc.All rights reserved.

//Redistribution and use in source and binary forms, with or without
//modification, are permitted provided that the following conditions are met:

//  1. Redistributions of source code must retain the above copyright notice,
//     this list of conditions and the following disclaimer.

//  2. Redistributions in binary form must reproduce the above copyright
//     notice, this list of conditions and the following disclaimer in 
//     the documentation and/or other materials provided with the distribution.

//  3. The names of the authors may not be used to endorse or promote products
//     derived from this software without specific prior written permission.

//THIS SOFTWARE IS PROVIDED ``AS IS'' AND ANY EXPRESSED OR IMPLIED WARRANTIES,
//INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
//FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.IN NO EVENT SHALL JCRAFT,
//INC.OR ANY CONTRIBUTORS TO THIS SOFTWARE BE LIABLE FOR ANY DIRECT, INDIRECT,
//INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
//LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA,
//OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
//LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT(INCLUDING
//NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
//EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
// ReSharper disable ArrangeThisQualifier
// ReSharper disable InconsistentNaming
namespace DotNetty.Codecs.Compression
{
    /// <summary>
    /// https://github.com/ymnk/jzlib/blob/master/src/main/java/com/jcraft/jzlib/Adler32.java
    /// </summary>
    sealed class Adler32 : IChecksum
    {
        // largest prime smaller than 65536
        const int BASE = 65521;
        // NMAX is the largest n such that 255n(n+1)/2 + (n+1)(BASE-1) <= 2^32-1
        const int NMAX = 5552;

        long s1 = 1L;
        long s2;

        public void Reset(long init)
        {
            s1 = init & 0xffff;
            s2 = (init >> 16) & 0xffff;
        }

        public void Reset()
        {
            s1 = 1L;
            s2 = 0L;
        }

        public long GetValue() => ((s2 << 16) | s1);

        public void Update(byte[] buf, int index, int len)
        {
            if (len == 1)
            {
                s1 += buf[index] & 0xff; s2 += s1;
                s1 %= BASE;
                s2 %= BASE;
                return;
            }

            int len1 = len / NMAX;
            int len2 = len % NMAX;
            while (len1-- > 0)
            {
                int k = NMAX;
                len -= k;
                while (k-- > 0)
                {
                    s1 += buf[index++] & 0xff; s2 += s1;
                }
                s1 %= BASE;
                s2 %= BASE;
            }

            int k0 = len2;
            while (k0-- > 0)
            {
                s1 += buf[index++] & 0xff; s2 += s1;
            }
            s1 %= BASE;
            s2 %= BASE;
        }

        public IChecksum Copy()
        {
            var foo = new Adler32();
            foo.s1 = this.s1;
            foo.s2 = this.s2;
            return foo;
        }

        // The following logic has come from zlib.1.2.
        internal static long Combine(long adler1, long adler2, long len2)
        {
            long BASEL = BASE;
            long sum1;
            long sum2;
            long rem;  // unsigned int

            rem = len2 % BASEL;
            sum1 = adler1 & 0xffffL;
            sum2 = rem * sum1;
            sum2 %= BASEL; // MOD(sum2);
            sum1 += (adler2 & 0xffffL) + BASEL - 1;
            sum2 += ((adler1 >> 16) & 0xffffL) + ((adler2 >> 16) & 0xffffL) + BASEL - rem;
            if (sum1 >= BASEL) sum1 -= BASEL;
            if (sum1 >= BASEL) sum1 -= BASEL;
            if (sum2 >= (BASEL << 1)) sum2 -= (BASEL << 1);
            if (sum2 >= BASEL) sum2 -= BASEL;
            return sum1 | (sum2 << 16);
        }
    }
}
