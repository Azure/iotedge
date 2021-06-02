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

    // Helper functions for doubles.
    class NumberDoubleHelper
    {
        const long kSignMask = -0x8000000000000000L;
        const long kExponentMask = 0x7FF0000000000000L;
        const long kSignificandMask = 0x000FFFFFFFFFFFFFL;
        const long kHiddenBit = 0x0010000000000000L;

        public static NumberDiyFp AsDiyFp(long d64)
        {
            Debug.Assert(!IsSpecial(d64));
            return new NumberDiyFp(Significand(d64), Exponent(d64));
        }

        // this->Significand() must not be 0.
        public static NumberDiyFp AsNormalizedDiyFp(long d64)
        {
            long f = Significand(d64);
            int e = Exponent(d64);

            Debug.Assert(f != 0);

            // The current double could be a denormal.
            while ((f & kHiddenBit) == 0)
            {
                f <<= 1;
                e--;
            }
            // Do the final shifts in one go. Don't forget the hidden bit (the '-1').
            f <<= NumberDiyFp.kSignificandSize - kSignificandSize - 1;
            e -= NumberDiyFp.kSignificandSize - kSignificandSize - 1;
            return new NumberDiyFp(f, e);
        }

        public static int Exponent(long d64)
        {
            if (IsDenormal(d64)) return kDenormalExponent;

            int biased_e = (int)(((d64 & kExponentMask) >> kSignificandSize) & 0xffffffffL);
            return biased_e - kExponentBias;
        }

        public static long Significand(long d64)
        {
            long significand = d64 & kSignificandMask;
            if (!IsDenormal(d64))
            {
                return significand + kHiddenBit;
            }
            else
            {
                return significand;
            }
        }

        // Returns true if the double is a denormal.
        public static bool IsDenormal(long d64)
        {
            return (d64 & kExponentMask) == 0L;
        }

        // We consider denormals not to be special.
        // Hence only Infinity and NaN are special.
        public static bool IsSpecial(long d64)
        {
            return (d64 & kExponentMask) == kExponentMask;
        }

        public static bool IsNan(long d64)
        {
            return ((d64 & kExponentMask) == kExponentMask) &&
                    ((d64 & kSignificandMask) != 0L);
        }


        public static bool IsInfinite(long d64)
        {
            return ((d64 & kExponentMask) == kExponentMask) &&
                    ((d64 & kSignificandMask) == 0L);
        }


        public static int Sign(long d64)
        {
            return (d64 & kSignMask) == 0L ? 1 : -1;
        }


        // Returns the two boundaries of first argument.
        // The bigger boundary (m_plus) is normalized. The lower boundary has the same
        // exponent as m_plus.
        public static void NormalizedBoundaries(long d64, NumberDiyFp m_minus, NumberDiyFp m_plus)
        {
            NumberDiyFp v = AsDiyFp(d64);
            bool significand_is_zero = (v.F() == kHiddenBit);
            m_plus.SetF((v.F() << 1) + 1);
            m_plus.SetE(v.E() - 1);
            m_plus.Normalize();
            if (significand_is_zero && v.E() != kDenormalExponent)
            {
                // The boundary is closer. Think of v = 1000e10 and v- = 9999e9.
                // Then the boundary (== (v - v-)/2) is not just at a distance of 1e9 but
                // at a distance of 1e8.
                // The only exception is for the smallest normal: the largest denormal is
                // at the same distance as its successor.
                // Note: denormals have the same exponent as the smallest normals.
                m_minus.SetF((v.F() << 2) - 1);
                m_minus.SetE(v.E() - 2);
            }
            else
            {
                m_minus.SetF((v.F() << 1) - 1);
                m_minus.SetE(v.E() - 1);
            }
            m_minus.SetF(m_minus.F() << (m_minus.E() - m_plus.E()));
            m_minus.SetE(m_plus.E());
        }

        private const int kSignificandSize = 52;  // Excludes the hidden bit.
        private const int kExponentBias = 0x3FF + kSignificandSize;
        private const int kDenormalExponent = -kExponentBias + 1;
    }
}
