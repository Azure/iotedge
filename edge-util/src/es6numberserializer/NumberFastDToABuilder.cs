/* -*- Mode: java; tab-width: 8; indent-tabs-mode: nil; c-basic-offset: 4 -*-
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

// Ported to C# from the Mozilla "Rhino" project by Anders Rundgren.

using System;

/// <summary>
/// This is an internal part of a ES6 compatible JSON Number serializer.
/// </summary>

namespace Org.Webpki.Es6NumberSerialization
{
    class NumberFastDToABuilder
    {
        // allocate buffer for generated digits + extra notation + padding zeroes
        char[] chars = new char[NumberFastDToA.kFastDtoaMaximalLength + 8];
        internal int end = 0;
        internal int point;
        bool formatted = false;

        public void Append(char c)
        {
            chars[end++] = c;
        }

        public void DecreaseLast()
        {
            chars[end - 1]--;
        }

        public void Reset()
        {
            end = 0;
            formatted = false;
        }

        public String Format()
        {
            if (!formatted)
            {
                // check for minus sign
                int firstDigit = chars[0] == '-' ? 1 : 0;
                int decPoint = point - firstDigit;
                if (decPoint < -5 || decPoint > 21)
                {
                    ToExponentialFormat(firstDigit, decPoint);
                } else
                {
                    ToFixedFormat(firstDigit, decPoint);
                }
                formatted = true;
            }
            return new String(chars, 0, end);

        }

        private void ArrayFill0(int from, int to)
        {
            while (from < to)
            {
                chars[from++] = '0';
            }
        }

        private void ToFixedFormat(int firstDigit, int decPoint)
        {
            if (point < end)
            {
                // insert decimal point
                if (decPoint > 0)
                {
                    // >= 1, split decimals and insert point
                    Array.Copy(chars, point, chars, point + 1, end - point);
                    chars[point] = '.';
                    end++;
                }
                else
                {
                    // < 1,
                    int target = firstDigit + 2 - decPoint;
                    Array.Copy(chars, firstDigit, chars, target, end - firstDigit);
                    chars[firstDigit] = '0';
                    chars[firstDigit + 1] = '.';
                    if (decPoint < 0)
                    {
                        ArrayFill0(firstDigit + 2, target);
                    }
                    end += 2 - decPoint;
                }
            }
            else if (point > end)
            {
                // large integer, add trailing zeroes
                ArrayFill0(end, point);
                end += point - end;
            }
        }

        private void ToExponentialFormat(int firstDigit, int decPoint)
        {
            if (end - firstDigit > 1)
            {
                // insert decimal point if more than one digit was produced
                int dot = firstDigit + 1;
                Array.Copy(chars, dot, chars, dot + 1, end - dot);
                chars[dot] = '.';
                end++;
            }
            chars[end++] = 'e';
            char sign = '+';
            int exp = decPoint - 1;
            if (exp < 0)
            {
                sign = '-';
                exp = -exp;
            }
            chars[end++] = sign;

            int charPos = exp > 99 ? end + 2 : exp > 9 ? end + 1 : end;
            end = charPos + 1;

            // code below is needed because Integer.getChars() is not internal
            for (;;)
            {
                int r = exp % 10;
                chars[charPos--] = digits[r];
                exp = exp / 10;
                if (exp == 0) break;
            }
        }

        readonly static char[] digits = 
        {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'
        };
    }
}
