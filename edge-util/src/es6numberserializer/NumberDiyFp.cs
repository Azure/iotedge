// Copyright 2010 the V8 project authors. All rights reserved.
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//
//     * Redistributions of source code must retain the above copyright
//       notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above
//       copyright notice, this list of conditions and the following
//       disclaimer in the documentation and/or other materials provided
//       with the distribution.
//     * Neither the name of Google Inc. nor the names of its
//       contributors may be used to endorse or promote products derived
//       from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

// Ported to Java from Mozilla's version of V8-dtoa by Hannes Wallnoefer.
// The original revision was 67d1049b0bf9 from the mozilla-central tree.

// Ported to C# from the Mozilla "Rhino" project by Anders Rundgren.

using System.Diagnostics;

/// <summary>
/// This is an internal part of a ES6 compatible JSON Number serializer.
/// </summary>

namespace Org.Webpki.Es6NumberSerialization
{
    // This "Do It Yourself Floating Point" class implements a floating-point number
    // with a uint64 significand and an int exponent. Normalized DiyFp numbers will
    // have the most significant bit of the significand set.
    // Multiplication and Subtraction do not normalize their results.
    // DiyFp are not designed to contain special doubles (NaN and Infinity).
    class NumberDiyFp
    {

        private long fv;
        private int ev;

        internal const int kSignificandSize = 64;
        internal const long kUint64MSB = -0x8000000000000000L;


        public NumberDiyFp()
        {
            this.fv = 0;
            this.ev = 0;
        }

        public NumberDiyFp(long f, int e)
        {
            this.fv = f;
            this.ev = e;
        }

        private static bool Uint64_gte(long a, long b)
        {
            // greater-or-equal for unsigned int64 in java-style...
            return (a == b) || ((a > b) ^ (a < 0) ^ (b < 0));
        }

        // this = this - other.
        // The exponents of both numbers must be the same and the significand of this
        // must be bigger than the significand of other.
        // The result will not be normalized.
        private void Subtract(NumberDiyFp other)
        {
            Debug.Assert(ev == other.ev);
            Debug.Assert(Uint64_gte(fv, other.fv));
            fv -= other.fv;
        }

        // Returns a - b.
        // The exponents of both numbers must be the same and this must be bigger
        // than other. The result will not be normalized.
        public static NumberDiyFp Minus(NumberDiyFp a, NumberDiyFp b)
        {
            NumberDiyFp result = new NumberDiyFp(a.fv, a.ev);
            result.Subtract(b);
            return result;
        }


        // this = this * other.
        private void Multiply(NumberDiyFp other)
        {
            // Simply "emulates" a 128 bit multiplication.
            // However: the resulting number only contains 64 bits. The least
            // significant 64 bits are only used for rounding the most significant 64
            // bits.
            const long kM32 = 0xFFFFFFFFL;
            long a = (long)((ulong)fv >> 32);
            long b = fv & kM32;
            long c = (long)((ulong)other.fv >> 32);
            long d = other.fv & kM32;
            long ac = a * c;
            long bc = b * c;
            long ad = a * d;
            long bd = b * d;
            long tmp = ((long)((ulong)bd >> 32)) + (ad & kM32) + (bc & kM32);
            // By adding 1U << 31 to tmp we round the final result.
            // Halfway cases will be round up.
            tmp += 1L << 31;
            long result_f = ac + ((long)((ulong)ad >> 32)) + ((long)((ulong)bc >> 32)) + ((long)((ulong)tmp >> 32));
            ev += other.ev + 64;
            fv = result_f;
        }

        // returns a * b;
        public static NumberDiyFp Times(NumberDiyFp a, NumberDiyFp b)
        {
            NumberDiyFp result = new NumberDiyFp(a.fv, a.ev);
            result.Multiply(b);
            return result;
        }

        public void Normalize()
        {
            Debug.Assert(fv != 0);
            long f = this.fv;
            int e = this.ev;

            // This method is mainly called for normalizing boundaries. In general
            // boundaries need to be shifted by 10 bits. We thus optimize for this case.
            const long k10MSBits = 0xFFC00000L << 32;
            while ((f & k10MSBits) == 0)
            {
                f <<= 10;
                e -= 10;
            }
            while ((f & kUint64MSB) == 0)
            {
                f <<= 1;
                e--;
            }
            this.fv = f;
            this.ev = e;
        }

        internal static NumberDiyFp Normalize(NumberDiyFp a)
        {
            NumberDiyFp result = new NumberDiyFp(a.fv, a.ev);
            result.Normalize();
            return result;
        }

        internal long F()
        {
            return fv;
        }

        internal int E()
        {
            return ev;
        }

        internal void SetF(long new_value)
        {
            fv = new_value;
        }

        internal void SetE(int new_value)
        {
            ev = new_value;
        }

    }
}
