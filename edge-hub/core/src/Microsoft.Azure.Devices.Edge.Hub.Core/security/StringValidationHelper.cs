// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;

    internal static class StringValidationHelper
    {
        private const char Base64Padding = '=';

        private static readonly HashSet<char> base64Table =
            new HashSet<char>
            {
                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O',
                'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd',
                'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's',
                't', 'u', 'v', 'w', 'x', 'y', 'z', '0', '1', '2', '3', '4', '5', '6', '7',
                '8', '9', '+', '/'
            };

        public static void EnsureBase64String(string value, string paramName)
        {
            if (!IsBase64StringValid(value))
            {
                throw new ArgumentException($"{value} is not a valid Base64 encoded string", paramName);
            }
        }

        public static bool IsBase64StringValid(string value)
        {
            if (value == null)
            {
                return false;
            }

            return IsBase64String(value);
        }

        public static void EnsureNullOrBase64String(string value, string paramName)
        {
            if (!IsNullOrBase64String(value))
            {
                throw new ArgumentException($"{value} is not a valid Base64 encoded string", paramName);
            }
        }

        public static bool IsNullOrBase64String(string value)
        {
            if (value == null)
            {
                return true;
            }

            return IsBase64String(value);
        }

        public static bool IsBase64String(string value)
        {
            value = value.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);

            if (value.Length == 0 || (value.Length % 4) != 0)
            {
                return false;
            }

            var lengthNoPadding = value.Length;
            value = value.TrimEnd(Base64Padding);
            var lengthPadding = value.Length;

            if ((lengthNoPadding - lengthPadding) > 2)
            {
                return false;
            }

            foreach (char c in value)
            {
                if (!base64Table.Contains(c))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
