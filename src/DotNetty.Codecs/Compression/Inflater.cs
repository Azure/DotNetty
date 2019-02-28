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
    /// https://github.com/ymnk/jzlib/blob/master/src/main/java/com/jcraft/jzlib/Inflater.java
    /// </summary>
    sealed class Inflater : ZStream
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

        public Inflater()
        {
            Init();
        }

        public Inflater(JZlib.WrapperType wrapperType) : this(DEF_WBITS, wrapperType)
        {
        }

        public Inflater(int w, JZlib.WrapperType wrapperType)
        {
            int ret = Init(w, wrapperType);
            if (ret != Z_OK) throw new GZIPException(ret + ": " + msg);
        }

        public Inflater(int w) : this(w, false)
        {
        }

        public Inflater(bool nowrap) : this(DEF_WBITS, nowrap)
        {
        }

        public Inflater(int w, bool nowrap)
        {
            int ret = Init(w, nowrap);
            if (ret != Z_OK) throw new GZIPException(ret + ": " + msg);
        }

        //bool finished = false;

        public int Init() => Init(DEF_WBITS);

        public int Init(JZlib.WrapperType wrapperType) => Init(DEF_WBITS, wrapperType);

        public int Init(int w, JZlib.WrapperType wrapperType)
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
                w |= Compression.Inflate.INFLATE_ANY;
            }
            else if (wrapperType == JZlib.W_ZLIB)
            {
            }
            return Init(w, nowrap);
        }

        public int Init(bool nowrap) => Init(DEF_WBITS, nowrap);

        public int Init(int w) => Init(w, false);

        public int Init(int w, bool nowrap)
        {
            //finished = false;
            istate = new Inflate(this);
            return istate.InflateInit(nowrap ? -w : w);
        }

        public int Inflate(int f)
        {
            if (istate == null) return Z_STREAM_ERROR;
            int ret = istate.Inflate_I(f);
            //if (ret == Z_STREAM_END)
            //    finished = true;
            return ret;
        }

        public override int End()
        {
            // finished = true;
            if (istate == null) return Z_STREAM_ERROR;
            int ret = istate.InflateEnd();
            //    istate = null;
            return ret;
        }

        public int Sync()
        {
            if (istate == null) return Z_STREAM_ERROR;
            return istate.InflateSync();
        }
        public int SyncPoint()
        {
            if (istate == null) return Z_STREAM_ERROR;
            return istate.InflateSyncPoint();
        }

        public int SetDictionary(byte[] dictionary, int dictLength)
        {
            if (istate == null) return Z_STREAM_ERROR;
            return istate.InflateSetDictionary(dictionary, dictLength);
        }

        public override bool Finished() => istate.mode == 12 /*DONE*/;
    }
}
