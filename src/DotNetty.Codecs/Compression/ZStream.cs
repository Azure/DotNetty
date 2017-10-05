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

    /// <summary>
    /// https://github.com/ymnk/jzlib/blob/master/src/main/java/com/jcraft/jzlib/ZStream.java
    /// </summary>

    class ZStream
    {
        const int MAX_WBITS = 15; // 32K LZ77 window
        const int DEF_WBITS = MAX_WBITS;

        //const int Z_NO_FLUSH = 0;
        //const int Z_PARTIAL_FLUSH = 1;
        //const int Z_SYNC_FLUSH = 2;
        //const int Z_FULL_FLUSH = 3;
        //const int Z_FINISH = 4;

        //const int MAX_MEM_LEVEL = 9;

        const int Z_OK = 0;
        //const int Z_STREAM_END = 1;
        //const int Z_NEED_DICT = 2;
        //const int Z_ERRNO = -1;
        const int Z_STREAM_ERROR = -2;
        //const int Z_DATA_ERROR = -3;
        //const int Z_MEM_ERROR = -4;
        //const int Z_BUF_ERROR = -5;
        //const int Z_VERSION_ERROR = -6;

        public byte[] next_in;     // next input byte
        public int next_in_index;
        public int avail_in;       // number of bytes available at next_in
        public long total_in;      // total nb of input bytes read so far

        public byte[] next_out;    // next output byte should be put there
        public int next_out_index;
        public int avail_out;      // remaining free space at next_out
        public long total_out;     // total nb of bytes output so far

        public string msg;

        internal Deflate dstate;
        internal Inflate istate;

        internal int data_type; // best guess about the data type: ascii or binary

        internal IChecksum adler;

        protected internal ZStream() : this(new Adler32())
        {
        }

        protected ZStream(IChecksum adler)
        {
            this.adler = adler;
        }

        internal int InflateInit() => InflateInit(DEF_WBITS);

        internal int InflateInit(bool nowrap) => InflateInit(DEF_WBITS, nowrap);

        internal int InflateInit(int w) => InflateInit(w, false);

        internal int InflateInit(JZlib.WrapperType wrapperType) => InflateInit(DEF_WBITS, wrapperType);

        internal int InflateInit(int w, JZlib.WrapperType wrapperType)
        {
            bool nowrap = false;
            if (wrapperType == JZlib.W_NONE)
            {
                nowrap = true;
            }
            else if (wrapperType == JZlib.W_GZIP)
            {
                w += 16;
            }
            else if (wrapperType == JZlib.W_ANY)
            {
                w |= Inflate.INFLATE_ANY;
            }
            else if (wrapperType == JZlib.W_ZLIB)
            {
            }
            return InflateInit(w, nowrap);
        }

        internal int InflateInit(int w, bool nowrap)
        {
            istate = new Inflate(this);
            return istate.InflateInit(nowrap ? -w : w);
        }

        internal int Inflate_z(int f)
        {
            if (istate == null) return Z_STREAM_ERROR;
            return istate.Inflate_I(f);
        }

        internal int InflateEnd()
        {
            if (istate == null) return Z_STREAM_ERROR;
            int ret = istate.InflateEnd();
            //    istate = null;
            return ret;
        }

        internal int InflateSync()
        {
            if (istate == null)
                return Z_STREAM_ERROR;
            return istate.InflateSync();
        }

        internal int InflateSyncPoint()
        {
            if (istate == null) return Z_STREAM_ERROR;
            return istate.InflateSyncPoint();
        }

        internal int InflateSetDictionary(byte[] dictionary, int dictLength)
        {
            if (istate == null) return Z_STREAM_ERROR;
            return istate.InflateSetDictionary(dictionary, dictLength);
        }

        internal bool InflateFinished() => this.istate.mode == 12;

        internal int DeflateInit(int level) => DeflateInit(level, MAX_WBITS);

        internal int DeflateInit(int level, bool nowrap) => DeflateInit(level, MAX_WBITS, nowrap);

        internal int DeflateInit(int level, int bits) => DeflateInit(level, bits, false);

        internal int DeflateInit(int level, int bits, int memlevel, JZlib.WrapperType wrapperType)
        {
            if (bits < 9 || bits > 15)
            {
                return Z_STREAM_ERROR;
            }
            if (wrapperType == JZlib.W_NONE)
            {
                bits *= -1;
            }
            else if (wrapperType == JZlib.W_GZIP)
            {
                bits += 16;
            }
            else if (wrapperType == JZlib.W_ANY)
            {
                return Z_STREAM_ERROR;
            }
            else if (wrapperType == JZlib.W_ZLIB)
            {
            }

            return DeflateInit(level, bits, memlevel);
        }

        internal int DeflateInit(int level, int bits, int memlevel)
        {
            dstate = new Deflate(this);
            return dstate.DeflateInit(level, bits, memlevel);
        }

        internal int DeflateInit(int level, int bits, bool nowrap)
        {
            dstate = new Deflate(this);
            return dstate.DeflateInit(level, nowrap ? -bits : bits);
        }

        internal int Deflate_z(int flush)
        {
            if (dstate == null) return Z_STREAM_ERROR;
            return dstate.Deflate_D(flush);
        }

        internal int DeflateEnd()
        {
            if (dstate == null) return Z_STREAM_ERROR;
            int ret = dstate.DeflateEnd();
            dstate = null;
            return ret;
        }

        internal int DeflateParams(int level, int strategy)
        {
            if (dstate == null) return Z_STREAM_ERROR;
            return dstate.DeflateParams(level, strategy);
        }

        internal int DeflateSetDictionary(byte[] dictionary, int dictLength)
        {
            if (dstate == null) return Z_STREAM_ERROR;
            return dstate.DeflateSetDictionary(dictionary, dictLength);
        }

        // Flush as much pending output as possible. All deflate() output goes
        // through this function so some applications may wish to modify it
        // to avoid allocating a large strm->next_out buffer and copying into it.
        // (See also read_buf()).
        internal void Flush_pending()
        {
            int len = dstate.pending;

            if (len > avail_out) len = avail_out;
            if (len == 0) return;

            if (dstate.pending_buf.Length <= dstate.pending_out ||
               next_out.Length <= next_out_index ||
               dstate.pending_buf.Length < (dstate.pending_out + len) ||
               next_out.Length < (next_out_index + len))
            {
                //System.out.println(dstate.pending_buf.length+", "+dstate.pending_out+
                //    ", "+next_out.length+", "+next_out_index+", "+len);
                //System.out.println("avail_out="+avail_out);
            }

            Array.Copy(dstate.pending_buf, dstate.pending_out,
                     next_out, next_out_index, len);

            next_out_index += len;
            dstate.pending_out += len;
            total_out += len;
            avail_out -= len;
            dstate.pending -= len;
            if (dstate.pending == 0)
            {
                dstate.pending_out = 0;
            }
        }

        // Read a new buffer from the current input stream, update the adler32
        // and total number of bytes read.  All deflate() input goes through
        // this function so some applications may wish to modify it to avoid
        // allocating a large strm->next_in buffer and copying from it.
        // (See also flush_pending()).
        internal int Read_buf(byte[] buf, int start, int size)
        {
            int len = avail_in;

            if (len > size) len = size;
            if (len == 0) return 0;

            avail_in -= len;

            if (dstate.wrap != 0)
            {
                adler.Update(next_in, next_in_index, len);
            }
            Array.Copy(next_in, next_in_index, buf, start, len);
            next_in_index += len;
            total_in += len;
            return len;
        }

        internal long GetAdler() => adler.GetValue();

        internal void Free()
        {
            next_in = null;
            next_out = null;
            msg = null;
        }

        internal void SetOutput(byte[] buf) => SetOutput(buf, 0, buf.Length);

        internal void SetOutput(byte[] buf, int off, int len)
        {
            next_out = buf;
            next_out_index = off;
            avail_out = len;
        }

        internal void SetInput(byte[] buf) => SetInput(buf, 0, buf.Length, false);

        internal void SetInput(byte[] buf, bool append) => SetInput(buf, 0, buf.Length, append);

        internal void SetInput(byte[] buf, int off, int len, bool append)
        {
            if (len <= 0 && append && next_in != null) return;

            if (avail_in > 0 && append)
            {
                var tmp = new byte[avail_in + len];
                Array.Copy(next_in, next_in_index, tmp, 0, avail_in);
                Array.Copy(buf, off, tmp, avail_in, len);
                next_in = tmp;
                next_in_index = 0;
                avail_in += len;
            }
            else
            {
                next_in = buf;
                next_in_index = off;
                avail_in = len;
            }
        }

        internal byte[] GetNextIn() => next_in;

        internal void SetNextIn(byte[] next_in_value) => next_in = next_in_value;

        internal int GetNextInIndex() => next_in_index;

        internal void SetNextInIndex(int next_in_index_value) => next_in_index = next_in_index_value;

        internal int GetAvailIn() => avail_in;

        internal void SetAvailIn(int avail_in_value) => avail_in = avail_in_value;

        internal byte[] GetNextOut() => next_out;

        internal void SetNextOut(byte[] next_out_value) => next_out = next_out_value;

        internal int GetNextOutIndex() => next_out_index;

        internal void SetNextOutIndex(int next_out_index_value) => next_out_index = next_out_index_value;

        internal int GetAvailOut() => avail_out;

        internal void SetAvailOut(int avail_out_value) => avail_out = avail_out_value;

        internal long GetTotalOut() => total_out;

        internal long GetTotalIn() => total_in;

        internal string GetMessage() => msg;

        /**
         * Those methods are expected to be override by Inflater and Deflater.
         * In the future, they will become abstract methods.
         */
        public virtual int End() => Z_OK;

        public virtual bool Finished() => false;
    }
}
