using System;
/*
 * $Id: InfCodes.cs,v 1.2 2008-05-10 09:35:40 bouncy Exp $
 *
Copyright (c) 2000,2001,2002,2003 ymnk, JCraft,Inc. All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

  1. Redistributions of source code must retain the above copyright notice,
     this list of conditions and the following disclaimer.

  2. Redistributions in binary form must reproduce the above copyright 
     notice, this list of conditions and the following disclaimer in 
     the documentation and/or other materials provided with the distribution.

  3. The names of the authors may not be used to endorse or promote products
     derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED ``AS IS'' AND ANY EXPRESSED OR IMPLIED WARRANTIES,
INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL JCRAFT,
INC. OR ANY CONTRIBUTORS TO THIS SOFTWARE BE LIABLE FOR ANY DIRECT, INDIRECT,
INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA,
OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
/*
 * This program is based on zlib-1.1.3, so all credit should go authors
 * Jean-loup Gailly(jloup@gzip.org) and Mark Adler(madler@alumni.caltech.edu)
 * and contributors of zlib.
 */

namespace Org.BouncyCastle.Utilities.Zlib
{
    public enum InflateCodeMode
    {
        /// <summary>
        /// x: set up for InflateCodeMode.LEN
        /// </summary>
        START = 0,
        /// <summary>
        /// i: get length/literal/eob next
        /// </summary>
        LEN = 1,
        /// <summary>
        /// i: getting length extra (have base)
        /// </summary>
        LENEXT = 2,
        /// <summary>
        /// i: get distance next
        /// </summary>
        DIST = 3,
        /// <summary>
        /// : getting distance extra
        /// </summary>
        DISTEXT = 4,
        /// <summary>
        /// o: copying bytes in window, waiting for space
        /// </summary>
        COPY = 5,
        /// <summary>
        /// o: got literal, waiting for output space
        /// </summary>
        LIT = 6,
        /// <summary>
        /// o: got eob, possibly still output waiting
        /// </summary>
        WASH = 7,
        /// <summary>
        /// x: got eob and all data flushed
        /// </summary>
        END = 8,
        /// <summary>
        /// x: got error
        /// </summary>
        BADCODE = 9
    }

    internal abstract class InfCodes
    {

        private static readonly int[] inflate_mask = {
                                                0x00000000, 0x00000001, 0x00000003, 0x00000007, 0x0000000f,
                                                0x0000001f, 0x0000003f, 0x0000007f, 0x000000ff, 0x000001ff,
                                                0x000003ff, 0x000007ff, 0x00000fff, 0x00001fff, 0x00003fff,
                                                0x00007fff, 0x0000ffff
                                            };

        // waiting for "i:"=input,
        //             "o:"=output,
        //             "x:"=nothing
        /// <summary>
        /// Current inflate_codes mode
        /// </summary>
        private InflateCodeMode _mode;

        // mode dependent information
        private int _len;

        private int[] _tree; // pointer into tree
        private int _treeIndex = 0;
        private int _need;   // bits needed

        private int _lit;

        // if EXT or InflateCodeMode.COPY, where and how much
        private int _get;              // bits to get for extra
        private int _dist;             // distance back to copy from

        private byte _lbits;           // ltree bits decoded per branch
        private byte _dbits;           // dtree bits decoder per branch
        private int[] _ltree;          // literal/length/eob tree
        private int _ltreeIndex;      // literal/length/eob tree
        private int[] _dtree;          // distance tree
        private int _dtreeIndex;      // distance tree

        internal void InitCodes(int bl, int bd, int[] tl, int tl_index, int[] td, int td_index, ZStream z)
        {
            this._mode = InflateCodeMode.START;
            this._lbits = (byte)bl;
            this._dbits = (byte)bd;
            this._ltree = tl;
            this._ltreeIndex = tl_index;
            this._dtree = td;
            this._dtreeIndex = td_index;
            this._tree = null;
        }

        internal ZLibStatus ProcessCodes(InfBlocks s, ZStream z, ZLibStatus r)
        {
            int j;              // temporary storage
            int tindex;         // temporary pointer
            int e;              // extra bits or operation
            int b = 0;            // bit buffer
            int k = 0;            // bits in bit buffer
            int p = 0;            // input data pointer
            int n;              // bytes available there
            int q;              // output window write pointer
            int m;              // bytes to end of window or read pointer
            int f;              // pointer to copy strings from

            // copy input/output information to locals (UPDATE macro restores)
            p = z.next_in_index; n = z.avail_in; b = s.bitb; k = s.bitk;
            q = s.write; m = q < s.read ? s.read - q - 1 : s.end - q;

            // process input and output based on current state
            while (true)
            {
                switch (this._mode)
                {
                    // waiting for "i:"=input, "o:"=output, "x:"=nothing
                    case InflateCodeMode.START:         // x: set up for InflateCodeMode.LEN
                        if (m >= 258 && n >= 10)
                        {

                            s.bitb = b; s.bitk = k;
                            z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                            s.write = q;
                            r = InflateFast(this._lbits, this._dbits,
                                this._ltree, this._ltreeIndex,
                                this._dtree, _dtreeIndex,
                                s, z);

                            p = z.next_in_index; n = z.avail_in; b = s.bitb; k = s.bitk;
                            q = s.write; m = q < s.read ? s.read - q - 1 : s.end - q;

                            if (r != ZLibStatus.Z_OK)
                            {
                                this._mode = r == ZLibStatus.Z_STREAM_END ? InflateCodeMode.WASH : InflateCodeMode.BADCODE;
                                break;
                            }
                        }
                        this._need = this._lbits;
                        this._tree = this._ltree;
                        this._treeIndex = this._ltreeIndex;

                        this._mode = InflateCodeMode.LEN;
                        goto case InflateCodeMode.LEN;
                    case InflateCodeMode.LEN:           // i: get length/literal/eob next
                        j = this._need;

                        while (k < (j))
                        {
                            if (n != 0) r = ZLibStatus.Z_OK;
                            else
                            {

                                s.bitb = b; s.bitk = k;
                                z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                                s.write = q;
                                return s.inflate_flush(z, r);
                            }
                            n--;
                            b |= (z.next_in[p++] & 0xff) << k;
                            k += 8;
                        }

                        tindex = (this._treeIndex + (b & inflate_mask[j])) * 3;

                        b >>= (this._tree[tindex + 1]);
                        k -= (this._tree[tindex + 1]);

                        e = this._tree[tindex];

                        if (e == 0)
                        {               // literal
                            this._lit = this._tree[tindex + 2];
                            this._mode = InflateCodeMode.LIT;
                            break;
                        }
                        if ((e & 16) != 0)
                        {          // length
                            this._get = e & 15;
                            this._len = this._tree[tindex + 2];
                            this._mode = InflateCodeMode.LENEXT;
                            break;
                        }
                        if ((e & 64) == 0)
                        {        // next table
                            this._need = e;
                            this._treeIndex = tindex / 3 + this._tree[tindex + 2];
                            break;
                        }
                        if ((e & 32) != 0)
                        {               // end of block
                            this._mode = InflateCodeMode.WASH;
                            break;
                        }
                        this._mode = InflateCodeMode.BADCODE;        // invalid code
                        z.msg = "invalid literal/length code";
                        r = ZLibStatus.Z_DATA_ERROR;

                        s.bitb = b; s.bitk = k;
                        z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                        s.write = q;
                        return s.inflate_flush(z, r);

                    case InflateCodeMode.LENEXT:        // i: getting length extra (have base)
                        j = this._get;

                        while (k < (j))
                        {
                            if (n != 0) r = ZLibStatus.Z_OK;
                            else
                            {

                                s.bitb = b; s.bitk = k;
                                z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                                s.write = q;
                                return s.inflate_flush(z, r);
                            }
                            n--; b |= (z.next_in[p++] & 0xff) << k;
                            k += 8;
                        }

                        this._len += (b & inflate_mask[j]);

                        b >>= j;
                        k -= j;

                        this._need = this._dbits;
                        this._tree = this._dtree;
                        this._treeIndex = _dtreeIndex;
                        this._mode = InflateCodeMode.DIST;
                        goto case InflateCodeMode.DIST;
                    case InflateCodeMode.DIST:          // i: get distance next
                        j = this._need;

                        while (k < (j))
                        {
                            if (n != 0) r = ZLibStatus.Z_OK;
                            else
                            {

                                s.bitb = b; s.bitk = k;
                                z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                                s.write = q;
                                return s.inflate_flush(z, r);
                            }
                            n--; b |= (z.next_in[p++] & 0xff) << k;
                            k += 8;
                        }

                        tindex = (this._treeIndex + (b & inflate_mask[j])) * 3;

                        b >>= this._tree[tindex + 1];
                        k -= this._tree[tindex + 1];

                        e = (this._tree[tindex]);
                        if ((e & 16) != 0)
                        {               // distance
                            this._get = e & 15;
                            this._dist = this._tree[tindex + 2];
                            this._mode = InflateCodeMode.DISTEXT;
                            break;
                        }
                        if ((e & 64) == 0)
                        {        // next table
                            this._need = e;
                            this._treeIndex = tindex / 3 + this._tree[tindex + 2];
                            break;
                        }
                        this._mode = InflateCodeMode.BADCODE;        // invalid code
                        z.msg = "invalid distance code";
                        r = ZLibStatus.Z_DATA_ERROR;

                        s.bitb = b; s.bitk = k;
                        z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                        s.write = q;
                        return s.inflate_flush(z, r);

                    case InflateCodeMode.DISTEXT:       // i: getting distance extra
                        j = this._get;

                        while (k < (j))
                        {
                            if (n != 0) r = ZLibStatus.Z_OK;
                            else
                            {

                                s.bitb = b; s.bitk = k;
                                z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                                s.write = q;
                                return s.inflate_flush(z, r);
                            }
                            n--; b |= (z.next_in[p++] & 0xff) << k;
                            k += 8;
                        }

                        this._dist += (b & inflate_mask[j]);

                        b >>= j;
                        k -= j;

                        this._mode = InflateCodeMode.COPY;
                        goto case InflateCodeMode.COPY;
                    case InflateCodeMode.COPY:          // o: copying bytes in window, waiting for space
                        f = q - this._dist;
                        while (f < 0)
                        {     // modulo window size-"while" instead
                            f += s.end;     // of "if" handles invalid distances
                        }
                        while (this._len != 0)
                        {

                            if (m == 0)
                            {
                                if (q == s.end && s.read != 0) { q = 0; m = q < s.read ? s.read - q - 1 : s.end - q; }
                                if (m == 0)
                                {
                                    s.write = q; r = s.inflate_flush(z, r);
                                    q = s.write; m = q < s.read ? s.read - q - 1 : s.end - q;

                                    if (q == s.end && s.read != 0) { q = 0; m = q < s.read ? s.read - q - 1 : s.end - q; }

                                    if (m == 0)
                                    {
                                        s.bitb = b; s.bitk = k;
                                        z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                                        s.write = q;
                                        return s.inflate_flush(z, r);
                                    }
                                }
                            }

                            s.window[q++] = s.window[f++]; m--;

                            if (f == s.end)
                                f = 0;
                            this._len--;
                        }
                        this._mode = InflateCodeMode.START;
                        break;
                    case InflateCodeMode.LIT:           // o: got literal, waiting for output space
                        if (m == 0)
                        {
                            if (q == s.end && s.read != 0) { q = 0; m = q < s.read ? s.read - q - 1 : s.end - q; }
                            if (m == 0)
                            {
                                s.write = q; r = s.inflate_flush(z, r);
                                q = s.write; m = q < s.read ? s.read - q - 1 : s.end - q;

                                if (q == s.end && s.read != 0) { q = 0; m = q < s.read ? s.read - q - 1 : s.end - q; }
                                if (m == 0)
                                {
                                    s.bitb = b; s.bitk = k;
                                    z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                                    s.write = q;
                                    return s.inflate_flush(z, r);
                                }
                            }
                        }
                        r = ZLibStatus.Z_OK;

                        s.window[q++] = (byte)this._lit; m--;

                        this._mode = InflateCodeMode.START;
                        break;
                    case InflateCodeMode.WASH:           // o: got eob, possibly more output
                        if (k > 7)
                        {        // return unused byte, if any
                            k -= 8;
                            n++;
                            p--;             // can always return one
                        }

                        s.write = q; r = s.inflate_flush(z, r);
                        q = s.write; m = q < s.read ? s.read - q - 1 : s.end - q;

                        if (s.read != s.write)
                        {
                            s.bitb = b; s.bitk = k;
                            z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                            s.write = q;
                            return s.inflate_flush(z, r);
                        }
                        this._mode = InflateCodeMode.END;
                        goto case InflateCodeMode.END;
                    case InflateCodeMode.END:
                        r = ZLibStatus.Z_STREAM_END;
                        s.bitb = b; s.bitk = k;
                        z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                        s.write = q;
                        return s.inflate_flush(z, r);

                    case InflateCodeMode.BADCODE:       // x: got error

                        r = ZLibStatus.Z_DATA_ERROR;

                        s.bitb = b; s.bitk = k;
                        z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                        s.write = q;
                        return s.inflate_flush(z, r);

                    default:
                        r = ZLibStatus.Z_STREAM_ERROR;

                        s.bitb = b; s.bitk = k;
                        z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                        s.write = q;
                        return s.inflate_flush(z, r);
                }
            }
        }

        /// <summary>
        /// Inflates the fast.
        /// </summary>
        /// <param name="bl">The bl.</param>
        /// <param name="bd">The bd.</param>
        /// <param name="tl">The tl.</param>
        /// <param name="tl_index">The tl_index.</param>
        /// <param name="td">The td.</param>
        /// <param name="td_index">The td_index.</param>
        /// <param name="s">The s.</param>
        /// <param name="z">The z.</param>
        /// <returns></returns>
        /// <remarks>
        /// Called with number of bytes left to write in window at least 258
        /// (the maximum string length) and number of input bytes available
        /// at least ten.  The ten bytes are six bytes for the longest length/
        /// distance pair plus four bytes for overloading the bit buffer.
        /// </remarks>
        private ZLibStatus InflateFast(int bl, int bd, int[] tl, int tl_index, int[] td, int td_index, InfBlocks s, ZStream z)
        {
            int t;                // temporary pointer
            int[] tp;             // temporary pointer
            int tp_index;         // temporary pointer
            int e;                // extra bits or operation
            int b;                // bit buffer
            int k;                // bits in bit buffer
            int p;                // input data pointer
            int n;                // bytes available there
            int q;                // output window write pointer
            int m;                // bytes to end of window or read pointer
            int ml;               // mask for literal/length tree
            int md;               // mask for distance tree
            int c;                // bytes to copy
            int d;                // distance back to copy from
            int r;                // copy source pointer

            int tp_index_t_3;     // (tp_index+t)*3

            // load input, output, bit values
            p = z.next_in_index; n = z.avail_in; b = s.bitb; k = s.bitk;
            q = s.write; m = q < s.read ? s.read - q - 1 : s.end - q;

            // initialize masks
            ml = inflate_mask[bl];
            md = inflate_mask[bd];

            // do until not enough input or output space for fast loop
            do
            {                          // assume called with m >= 258 && n >= 10
                // get literal/length code
                while (k < (20))
                {              // max bits for literal/length code
                    n--;
                    b |= (z.next_in[p++] & 0xff) << k; k += 8;
                }

                t = b & ml;
                tp = tl;
                tp_index = tl_index;
                tp_index_t_3 = (tp_index + t) * 3;
                if ((e = tp[tp_index_t_3]) == 0)
                {
                    b >>= (tp[tp_index_t_3 + 1]); k -= (tp[tp_index_t_3 + 1]);

                    s.window[q++] = (byte)tp[tp_index_t_3 + 2];
                    m--;
                    continue;
                }
                do
                {

                    b >>= (tp[tp_index_t_3 + 1]); k -= (tp[tp_index_t_3 + 1]);

                    if ((e & 16) != 0)
                    {
                        e &= 15;
                        c = tp[tp_index_t_3 + 2] + ((int)b & inflate_mask[e]);

                        b >>= e; k -= e;

                        // decode distance base of block to copy
                        while (k < (15))
                        {           // max bits for distance code
                            n--;
                            b |= (z.next_in[p++] & 0xff) << k; k += 8;
                        }

                        t = b & md;
                        tp = td;
                        tp_index = td_index;
                        tp_index_t_3 = (tp_index + t) * 3;
                        e = tp[tp_index_t_3];

                        do
                        {

                            b >>= (tp[tp_index_t_3 + 1]); k -= (tp[tp_index_t_3 + 1]);

                            if ((e & 16) != 0)
                            {
                                // get extra bits to add to distance base
                                e &= 15;
                                while (k < (e))
                                {         // get extra bits (up to 13)
                                    n--;
                                    b |= (z.next_in[p++] & 0xff) << k; k += 8;
                                }

                                d = tp[tp_index_t_3 + 2] + (b & inflate_mask[e]);

                                b >>= (e); k -= (e);

                                // do the copy
                                m -= c;
                                if (q >= d)
                                {                // offset before dest
                                    //  just copy
                                    r = q - d;
                                    if (q - r > 0 && 2 > (q - r))
                                    {
                                        s.window[q++] = s.window[r++]; // minimum count is three,
                                        s.window[q++] = s.window[r++]; // so unroll loop a little
                                        c -= 2;
                                    }
                                    else
                                    {
                                        System.Array.Copy(s.window, r, s.window, q, 2);
                                        q += 2; r += 2; c -= 2;
                                    }
                                }
                                else
                                {                  // else offset after destination
                                    r = q - d;
                                    do
                                    {
                                        r += s.end;          // force pointer in window
                                    } while (r < 0);         // covers invalid distances
                                    e = s.end - r;
                                    if (c > e)
                                    {             // if source crosses,
                                        c -= e;              // wrapped copy
                                        if (q - r > 0 && e > (q - r))
                                        {
                                            do { s.window[q++] = s.window[r++]; }
                                            while (--e != 0);
                                        }
                                        else
                                        {
                                            System.Array.Copy(s.window, r, s.window, q, e);
                                            q += e; r += e; e = 0;
                                        }
                                        r = 0;                  // copy rest from start of window
                                    }

                                }

                                // copy all or what's left
                                if (q - r > 0 && c > (q - r))
                                {
                                    do { s.window[q++] = s.window[r++]; }
                                    while (--c != 0);
                                }
                                else
                                {
                                    System.Array.Copy(s.window, r, s.window, q, c);
                                    q += c; r += c; c = 0;
                                }
                                break;
                            }
                            else if ((e & 64) == 0)
                            {
                                t += tp[tp_index_t_3 + 2];
                                t += (b & inflate_mask[e]);
                                tp_index_t_3 = (tp_index + t) * 3;
                                e = tp[tp_index_t_3];
                            }
                            else
                            {
                                z.msg = "invalid distance code";

                                c = z.avail_in - n; c = (k >> 3) < c ? k >> 3 : c; n += c; p -= c; k -= c << 3;

                                s.bitb = b; s.bitk = k;
                                z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                                s.write = q;

                                return ZLibStatus.Z_DATA_ERROR;
                            }
                        }
                        while (true);
                        break;
                    }

                    if ((e & 64) == 0)
                    {
                        t += tp[tp_index_t_3 + 2];
                        t += (b & inflate_mask[e]);
                        tp_index_t_3 = (tp_index + t) * 3;
                        if ((e = tp[tp_index_t_3]) == 0)
                        {

                            b >>= (tp[tp_index_t_3 + 1]); k -= (tp[tp_index_t_3 + 1]);

                            s.window[q++] = (byte)tp[tp_index_t_3 + 2];
                            m--;
                            break;
                        }
                    }
                    else if ((e & 32) != 0)
                    {

                        c = z.avail_in - n; c = (k >> 3) < c ? k >> 3 : c; n += c; p -= c; k -= c << 3;

                        s.bitb = b; s.bitk = k;
                        z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                        s.write = q;

                        return ZLibStatus.Z_STREAM_END;
                    }
                    else
                    {
                        z.msg = "invalid literal/length code";

                        c = z.avail_in - n; c = (k >> 3) < c ? k >> 3 : c; n += c; p -= c; k -= c << 3;

                        s.bitb = b; s.bitk = k;
                        z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
                        s.write = q;

                        return ZLibStatus.Z_DATA_ERROR;
                    }
                }
                while (true);
            }
            while (m >= 258 && n >= 10);

            // not enough input or output--restore pointers and return
            c = z.avail_in - n; c = (k >> 3) < c ? k >> 3 : c; n += c; p -= c; k -= c << 3;

            s.bitb = b; s.bitk = k;
            z.avail_in = n; z.total_in += p - z.next_in_index; z.next_in_index = p;
            s.write = q;

            return ZLibStatus.Z_OK;
        }
    }
}