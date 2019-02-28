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
    using System;
    using System.IO;

    /// <summary>
    /// https://github.com/ymnk/jzlib/blob/master/src/main/java/com/jcraft/jzlib/Inflate.java
    /// </summary>
    sealed class Inflate
    {
        //const int MAX_WBITS = 15; // 32K LZ77 window

        // preset dictionary flag in zlib header
        const int PRESET_DICT = 0x20;

        //const int Z_NO_FLUSH = 0;
        //const int Z_PARTIAL_FLUSH = 1;
        //const int Z_SYNC_FLUSH = 2;
        //const int Z_FULL_FLUSH = 3;
        const int Z_FINISH = 4;

        const int Z_DEFLATED = 8;

        const int Z_OK = 0;
        const int Z_STREAM_END = 1;
        const int Z_NEED_DICT = 2;
        //const int Z_ERRNO = -1;
        const int Z_STREAM_ERROR = -2;
        const int Z_DATA_ERROR = -3;
        //const int Z_MEM_ERROR = -4;
        const int Z_BUF_ERROR = -5;
        //const int Z_VERSION_ERROR = -6;

        //const int METHOD = 0; // waiting for method byte
        //const int FLAG = 1; // waiting for flag byte
        const int DICT4 = 2; // four dictionary check bytes to go
        const int DICT3 = 3; // three dictionary check bytes to go
        const int DICT2 = 4; // two dictionary check bytes to go
        const int DICT1 = 5; // one dictionary check byte to go
        const int DICT0 = 6; // waiting for inflateSetDictionary
        const int BLOCKS = 7; // decompressing blocks
        const int CHECK4 = 8; // four check bytes to go
        const int CHECK3 = 9; // three check bytes to go
        const int CHECK2 = 10; // two check bytes to go
        const int CHECK1 = 11; // one check byte to go
        const int DONE = 12; // finished check, done
        const int BAD = 13; // got an error--stay here

        const int HEAD = 14;
        const int LENGTH = 15;
        const int TIME = 16;
        const int OS = 17;
        const int EXLEN = 18;
        const int EXTRA = 19;
        const int NAME = 20;
        const int COMMENT = 21;
        const int HCRC = 22;
        const int FLAGS = 23;

        internal static readonly int INFLATE_ANY = 0x40000000;

        internal int mode; // current inflate mode

        // mode dependent information
        int method; // if FLAGS, method byte

        // if CHECK, check values to compare
        long was = -1; // computed check value
        long need; // stream check value

        // if BAD, inflateSync's marker bytes count
        int marker;

        // mode independent information
        internal int wrap; // flag for no wrapper
        // 0: no wrapper
        // 1: zlib header
        // 2: gzip header
        // 4: auto detection

        int wbits; // log2(window size)  (8..15, defaults to 15)

        InfBlocks blocks; // current inflate_blocks state

        readonly ZStream z;

        int flags;

        int need_bytes = -1;
        byte[] crcbuf = new byte[4];

        GZIPHeader gheader = null;

        internal int InflateReset()
        {
            if (z == null)
                return Z_STREAM_ERROR;

            z.total_in = z.total_out = 0;
            z.msg = null;
            this.mode = HEAD;
            this.need_bytes = -1;
            this.blocks.Reset();
            return Z_OK;
        }

        internal int InflateEnd()
        {
            if (blocks != null)
            {
                blocks.Free();
            }
            return Z_OK;
        }

        internal Inflate(ZStream z)
        {
            this.z = z;
        }

        internal int InflateInit(int w)
        {
            z.msg = null;
            blocks = null;

            // handle undocumented wrap option (no zlib header or check)
            wrap = 0;
            if (w < 0)
            {
                w = -w;
            }
            else if ((w & INFLATE_ANY) != 0)
            {
                wrap = 4;
                w &= ~INFLATE_ANY;
                if (w < 48)
                    w &= 15;
            }
            else if ((w & ~31) != 0) // for example, DEF_WBITS + 32
            {
                wrap = 4; // zlib and gzip wrapped data should be accepted.
                w &= 15;
            }
            else
            {
                wrap = (w >> 4) + 1;
                if (w < 48)
                    w &= 15;
            }

            if (w < 8 || w > 15)
            {
                InflateEnd();
                return Z_STREAM_ERROR;
            }
            if (blocks != null && wbits != w)
            {
                blocks.Free();
                blocks = null;
            }

            // set window size
            wbits = w;

            this.blocks = new InfBlocks(z, 1 << w);

            // reset state
            InflateReset();

            return Z_OK;
        }

        internal int Inflate_I(int f)
        {
            //int hold = 0;

            int r;
            int b;

            if (z == null || z.next_in == null)
            {
                if (f == Z_FINISH && this.mode == HEAD)
                    return Z_OK;
                return Z_STREAM_ERROR;
            }

            f = f == Z_FINISH ? Z_BUF_ERROR : Z_OK;
            r = Z_BUF_ERROR;
            while (true)
            {
                if (mode == HEAD)
                {
                    if (wrap == 0)
                    {
                        this.mode = BLOCKS;
                        continue; // break;
                    }

                    try { r = ReadBytes(2, r, f); }
                    catch (Return e) { return e.r; }

                    if ((wrap == 4 || (wrap & 2) != 0) &&
                        this.need == 0x8b1fL) // gzip header
                    {
                        if (wrap == 4)
                        {
                            wrap = 2;
                        }
                        z.adler = new CRC32();
                        Checksum(2, this.need);

                        if (gheader == null)
                            gheader = new GZIPHeader();

                        this.mode = FLAGS;
                        continue; // break;
                    }

                    if ((wrap & 2) != 0)
                    {
                        this.mode = BAD;
                        z.msg = "incorrect header check";
                        continue; // break;
                    }

                    flags = 0;

                    this.method = ((int)this.need) & 0xff;
                    b = ((int)(this.need >> 8)) & 0xff;

                    if (((wrap & 1) == 0 || // check if zlib header allowed
                            (((this.method << 8) + b) % 31) != 0) &&
                        (this.method & 0xf) != Z_DEFLATED)
                    {
                        if (wrap == 4)
                        {
                            z.next_in_index -= 2;
                            z.avail_in += 2;
                            z.total_in -= 2;
                            wrap = 0;
                            this.mode = BLOCKS;
                            continue; // break;
                        }
                        this.mode = BAD;
                        z.msg = "incorrect header check";
                        // since zlib 1.2, it is allowted to inflateSync for this case.
                        /*
                        this.marker = 5;       // can't try inflateSync
                        */
                        continue; // break;
                    }

                    if ((this.method & 0xf) != Z_DEFLATED)
                    {
                        this.mode = BAD;
                        z.msg = "unknown compression method";
                        // since zlib 1.2, it is allowted to inflateSync for this case.
                        /*
                            this.marker = 5;       // can't try inflateSync
                        */
                        continue; // break;
                    }

                    if (wrap == 4)
                    {
                        wrap = 1;
                    }

                    if ((this.method >> 4) + 8 > this.wbits)
                    {
                        this.mode = BAD;
                        z.msg = "invalid window size";
                        // since zlib 1.2, it is allowted to inflateSync for this case.
                        /*
                            this.marker = 5;       // can't try inflateSync
                        */
                        continue; // break;
                    }

                    z.adler = new Adler32();

                    if ((b & PRESET_DICT) == 0)
                    {
                        this.mode = BLOCKS;
                        continue; // break;
                    }
                    this.mode = DICT4;

                } // case HEAD
                if (this.mode == DICT4)
                {
                    if (z.avail_in == 0)
                        return r;
                    r = f;

                    z.avail_in--;
                    z.total_in++;
                    this.need = ((z.next_in[z.next_in_index++] & 0xff) << 24) & 0xff000000L;
                    this.mode = DICT3;
                } // case DICT4
                if (this.mode == DICT3)
                {
                    if (z.avail_in == 0)
                        return r;
                    r = f;

                    z.avail_in--;
                    z.total_in++;
                    this.need += ((z.next_in[z.next_in_index++] & 0xff) << 16) & 0xff0000L;
                    this.mode = DICT2;

                } // case DICT3
                if (this.mode == DICT2)
                {
                    if (z.avail_in == 0)
                        return r;
                    r = f;

                    z.avail_in--;
                    z.total_in++;
                    this.need += ((z.next_in[z.next_in_index++] & 0xff) << 8) & 0xff00L;
                    this.mode = DICT1;

                } // case DICT2
                if (this.mode == DICT1)
                {
                    if (z.avail_in == 0)
                        return r;
                    r = f;

                    z.avail_in--;
                    z.total_in++;
                    this.need += (z.next_in[z.next_in_index++] & 0xffL);
                    z.adler.Reset(this.need);
                    this.mode = DICT0;
                    return Z_NEED_DICT;
                } // case DICT1
                if (this.mode == DICT0)
                {
                    this.mode = BAD;
                    z.msg = "need dictionary";
                    this.marker = 0; // can try inflateSync
                    return Z_STREAM_ERROR;
                } // case DICT0
                if (this.mode == BLOCKS)
                {
                    r = this.blocks.Proc(r);
                    if (r == Z_DATA_ERROR)
                    {
                        this.mode = BAD;
                        this.marker = 0; // can try inflateSync
                        continue; // break;
                    }
                    if (r == Z_OK)
                    {
                        r = f;
                    }
                    if (r != Z_STREAM_END)
                    {
                        return r;
                    }
                    r = f;
                    this.was = z.adler.GetValue();
                    this.blocks.Reset();
                    if (this.wrap == 0)
                    {
                        this.mode = DONE;
                        continue; // break;
                    }
                    this.mode = CHECK4;
                } // case BLOCKS
                if (this.mode == CHECK4)
                {
                    if (z.avail_in == 0)
                        return r;
                    r = f;

                    z.avail_in--;
                    z.total_in++;
                    this.need = ((z.next_in[z.next_in_index++] & 0xff) << 24) & 0xff000000L;
                    this.mode = CHECK3;
                } // case CHECK4
                if (this.mode == CHECK3)
                {
                    if (z.avail_in == 0)
                        return r;
                    r = f;

                    z.avail_in--;
                    z.total_in++;
                    this.need += ((z.next_in[z.next_in_index++] & 0xff) << 16) & 0xff0000L;
                    this.mode = CHECK2;
                } // case CHECK3
                if (this.mode == CHECK2)
                {
                    if (z.avail_in == 0)
                        return r;
                    r = f;

                    z.avail_in--;
                    z.total_in++;
                    this.need += ((z.next_in[z.next_in_index++] & 0xff) << 8) & 0xff00L;
                    this.mode = CHECK1;
                } // case CHECK2
                if (this.mode == CHECK1)
                {
                    if (z.avail_in == 0)
                        return r;
                    r = f;

                    z.avail_in--;
                    z.total_in++;
                    this.need += (z.next_in[z.next_in_index++] & 0xffL);

                    if (flags != 0)
                    {
                        // gzip
                        this.need = ((this.need & 0xff000000) >> 24 |
                            (this.need & 0x00ff0000) >> 8 |
                            (this.need & 0x0000ff00) << 8 |
                            (this.need & 0x0000ffff) << 24) & 0xffffffffL;
                    }

                    if (((int)(this.was)) != ((int)(this.need)))
                    {
                        z.msg = "incorrect data check";
                        // chack is delayed
                        /*
                        this.mode = BAD;
                        this.marker = 5;       // can't try inflateSync
                        break;
                    */
                    }
                    else if (flags != 0 && gheader != null)
                    {
                        gheader.crc = this.need;
                    }

                    this.mode = LENGTH;
                } // case CHECK1
                if (this.mode == LENGTH)
                {
                    if (wrap != 0 && flags != 0)
                    {
                        try  { r = ReadBytes(4, r, f); }
                        catch (Return e) { return e.r; }

                        if (z.msg != null && z.msg.Equals("incorrect data check"))
                        {
                            this.mode = BAD;
                            this.marker = 5; // can't try inflateSync
                            continue; // break;
                        }

                        if (this.need != (z.total_out & 0xffffffffL))
                        {
                            z.msg = "incorrect length check";
                            this.mode = BAD;
                            continue; // break;
                        }
                        z.msg = null;
                    }
                    else
                    {
                        if (z.msg != null && z.msg.Equals("incorrect data check"))
                        {
                            this.mode = BAD;
                            this.marker = 5; // can't try inflateSync
                            continue; // break;
                        }
                    }

                    this.mode = DONE;
                } // case LENGTH
                if (this.mode == DONE)
                {
                    return Z_STREAM_END;
                }
                if (this.mode == BAD)
                {
                    return Z_DATA_ERROR;
                }
                if (this.mode == FLAGS)
                {
                    try  { r = ReadBytes(2, r, f); }
                    catch (Return e) { return e.r; }

                    flags = ((int)this.need) & 0xffff;

                    if ((flags & 0xff) != Z_DEFLATED)
                    {
                        z.msg = "unknown compression method";
                        this.mode = BAD;
                        continue; // break;
                    }
                    if ((flags & 0xe000) != 0)
                    {
                        z.msg = "unknown header flags set";
                        this.mode = BAD;
                        continue; // break;
                    }

                    if ((flags & 0x0200) != 0)
                    {
                        Checksum(2, this.need);
                    }

                    this.mode = TIME;
                } // case FLAGS
                if (this.mode == TIME)
                {
                    try
                    {
                        r = ReadBytes(4, r, f);
                    }
                    catch (Return e)
                    {
                        return e.r;
                    }
                    if (gheader != null)
                        gheader.time = this.need;
                    if ((flags & 0x0200) != 0)
                    {
                        Checksum(4, this.need);
                    }
                    this.mode = OS;

                } // case TIME
                if (this.mode == OS)
                {
                    try
                    {
                        r = ReadBytes(2, r, f);
                    }
                    catch (Return e)
                    {
                        return e.r;
                    }
                    if (gheader != null)
                    {
                        gheader.xflags = ((int)this.need) & 0xff;
                        gheader.os = (((int)this.need) >> 8) & 0xff;
                    }
                    if ((flags & 0x0200) != 0)
                    {
                        Checksum(2, this.need);
                    }
                    this.mode = EXLEN;

                } // case OS
                if (this.mode == EXLEN)
                {
                    if ((flags & 0x0400) != 0)
                    {
                        try
                        {
                            r = ReadBytes(2, r, f);
                        }
                        catch (Return e)
                        {
                            return e.r;
                        }
                        if (gheader != null)
                        {
                            gheader.extra = new byte[((int)this.need) & 0xffff];
                        }
                        if ((flags & 0x0200) != 0)
                        {
                            Checksum(2, this.need);
                        }
                    }
                    else if (gheader != null)
                    {
                        gheader.extra = null;
                    }
                    this.mode = EXTRA;

                } // case EXLEN
                if (this.mode == EXTRA)
                {
                    if ((flags & 0x0400) != 0)
                    {
                        try
                        {
                            r = ReadBytes(r, f);
                            if (gheader != null)
                            {
                                byte[] foo = tmp_string.ToArray();
                                tmp_string = null;
                                if (foo.Length == gheader.extra.Length)
                                {
                                    Array.Copy(foo, 0, gheader.extra, 0, foo.Length);
                                }
                                else
                                {
                                    z.msg = "bad extra field length";
                                    this.mode = BAD;
                                    continue; // break;
                                }
                            }
                        }
                        catch (Return e)
                        {
                            return e.r;
                        }
                    }
                    else if (gheader != null)
                    {
                        gheader.extra = null;
                    }
                    this.mode = NAME;
                } // case EXTRA
                if (this.mode == NAME)
                {
                    if ((flags & 0x0800) != 0)
                    {
                        try
                        {
                            r = ReadString(r, f);
                            if (gheader != null)
                            {
                                gheader.name = tmp_string.ToArray();
                            }
                            tmp_string = null;
                        }
                        catch (Return e)
                        {
                            return e.r;
                        }
                    }
                    else if (gheader != null)
                    {
                        gheader.name = null;
                    }
                    this.mode = COMMENT;

                } // case NAME
                if (this.mode == COMMENT)
                {
                    if ((flags & 0x1000) != 0)
                    {
                        try
                        {
                            r = ReadString(r, f);
                            if (gheader != null)
                            {
                                gheader.comment = tmp_string.ToArray();
                            }
                            tmp_string = null;
                        }
                        catch (Return e)
                        {
                            return e.r;
                        }
                    }
                    else if (gheader != null)
                    {
                        gheader.comment = null;
                    }
                    this.mode = HCRC;
                } // case COMMENT
                if (this.mode == HCRC)
                {
                    if ((flags & 0x0200) != 0)
                    {
                        try { r = ReadBytes(2, r, f); }
                        catch (Return e) { return e.r; }
                        if (gheader != null)
                        {
                            gheader.hcrc = (int)(this.need & 0xffff);
                        }
                        if (this.need != (z.adler.GetValue() & 0xffffL))
                        {
                            this.mode = BAD;
                            z.msg = "header crc mismatch";
                            this.marker = 5; // can't try inflateSync
                            continue; // break;
                        }
                    }
                    z.adler = new CRC32();
                    this.mode = BLOCKS;
                    continue; // break;
                } // case HCRC

                // BAD
                // Default
                break;
            }

            // Default
            return Z_STREAM_ERROR;
        }

        internal int InflateSetDictionary(byte[] dictionary, int dictLength)
        {
            if (z == null || (this.mode != DICT0 && this.wrap != 0))
            {
                return Z_STREAM_ERROR;
            }

            int index = 0;
            int length = dictLength;

            if (this.mode == DICT0)
            {
                long adler_need = z.adler.GetValue();
                z.adler.Reset();
                z.adler.Update(dictionary, 0, dictLength);
                if (z.adler.GetValue() != adler_need)
                {
                    return Z_DATA_ERROR;
                }
            }

            z.adler.Reset();

            if (length >= (1 << this.wbits))
            {
                length = (1 << this.wbits) - 1;
                index = dictLength - length;
            }
            this.blocks.Set_dictionary(dictionary, index, length);
            this.mode = BLOCKS;
            return Z_OK;
        }

        static byte[] mark = { (byte)0, (byte)0, (byte)0xff, (byte)0xff };

        internal int InflateSync()
        {
            int n; // number of bytes to look at
            int p; // pointer to bytes
            int m; // number of marker bytes found in a row
            long r, w; // temporaries to save total_in and total_out

            // set up
            if (z == null)
                return Z_STREAM_ERROR;
            if (this.mode != BAD)
            {
                this.mode = BAD;
                this.marker = 0;
            }
            if ((n = z.avail_in) == 0)
                return Z_BUF_ERROR;

            p = z.next_in_index;
            m = this.marker;
            // search
            while (n != 0 && m < 4)
            {
                if (z.next_in[p] == mark[m])
                {
                    m++;
                }
                else if (z.next_in[p] != 0)
                {
                    m = 0;
                }
                else
                {
                    m = 4 - m;
                }
                p++;
                n--;
            }

            // restore
            z.total_in += p - z.next_in_index;
            z.next_in_index = p;
            z.avail_in = n;
            this.marker = m;

            // return no joy or set up to restart on a new block
            if (m != 4)
            {
                return Z_DATA_ERROR;
            }
            r = z.total_in;
            w = z.total_out;
            InflateReset();
            z.total_in = r;
            z.total_out = w;
            this.mode = BLOCKS;

            return Z_OK;
        }

        // Returns true if inflate is currently at the end of a block generated
        // by Z_SYNC_FLUSH or Z_FULL_FLUSH. This function is used by one PPP
        // implementation to provide an additional safety check. PPP uses Z_SYNC_FLUSH
        // but removes the length bytes of the resulting empty stored block. When
        // decompressing, PPP checks that at the end of input packet, inflate is
        // waiting for these length bytes.
        internal int InflateSyncPoint()
        {
            if (z == null || this.blocks == null)
                return Z_STREAM_ERROR;
            return this.blocks.Sync_point();
        }

        int ReadBytes(int n, int r, int f)
        {
            if (need_bytes == -1)
            {
                need_bytes = n;
                this.need = 0;
            }
            while (need_bytes > 0)
            {
                if (z.avail_in == 0) { throw new Return(r); }
                r = f;
                z.avail_in--; z.total_in++;
                this.need = this.need |
                    (long)((z.next_in[z.next_in_index++] & 0xff) << ((n - need_bytes) * 8));
                need_bytes--;
            }
            if (n == 2)
            {
                this.need &= 0xffffL;
            }
            else if (n == 4)
            {
                this.need &= 0xffffffffL;
            }
            need_bytes = -1;
            return r;
        }

        class Return : Exception
        {
            internal int r;

            internal Return(int r) { this.r = r; }
        }

        MemoryStream tmp_string = null;

        int ReadString(int r, int f)
        {
            if (tmp_string == null)
            {
                tmp_string = new MemoryStream();
            }
            int b = 0;
            do
            {
                if (z.avail_in == 0)
                {
                    throw new Return(r);
                }
                ;
                r = f;
                z.avail_in--;
                z.total_in++;
                b = z.next_in[z.next_in_index];
                if (b != 0)
                    tmp_string.Write(z.next_in, z.next_in_index, 1);
                z.adler.Update(z.next_in, z.next_in_index, 1);
                z.next_in_index++;
            }
            while (b != 0);
            return r;
        }

        int ReadBytes(int r, int f)
        {
            if (tmp_string == null)
            {
                tmp_string = new MemoryStream();
            }
            int b = 0;
            while (this.need > 0)
            {
                if (z.avail_in == 0)
                {
                    throw new Return(r);
                }
                ;
                r = f;
                z.avail_in--;
                z.total_in++;
                b = z.next_in[z.next_in_index];
                tmp_string.Write(z.next_in, z.next_in_index, 1);
                z.adler.Update(z.next_in, z.next_in_index, 1);
                z.next_in_index++;
                this.need--;
            }
            return r;
        }

        void Checksum(int n, long v)
        {
            for (int i = 0; i < n; i++)
            {
                crcbuf[i] = (byte)(v & 0xff);
                v >>= 8;
            }
            z.adler.Update(crcbuf, 0, n);
        }

        public GZIPHeader getGZIPHeader() => this.gheader;

        internal bool InParsingHeader()
        {
            switch (mode)
            {
                case HEAD:
                case DICT4:
                case DICT3:
                case DICT2:
                case DICT1:
                case FLAGS:
                case TIME:
                case OS:
                case EXLEN:
                case EXTRA:
                case NAME:
                case COMMENT:
                case HCRC:
                    return true;
                default:
                    return false;
            }
        }
    }
}