// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;

    public static class StringEx
    {
        public static IEnumerable<string> Chunks(this string self, int size)
        {
            Preconditions.CheckNotNull(self, nameof(self));
            Preconditions.CheckRange(size, 0, nameof(size));

            var length = self.Length;
            for (int i = 0; i < length; i += size)
            {
                yield return self.Substring(i, Math.Min(size, length - i));
            }
        }

        public static string Join(this IEnumerable<string> strings) => strings.Join("");

        public static string Join(this IEnumerable<string> strings, string separator) => string.Join(separator, strings);
    }
}
