// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;

    public static class StringEx
    {
        /// <summary>
        /// Adds an extension method to string that returns an iterator
        /// of strings of a specific length.
        /// </summary>
        /// <param name="self"></param>
        /// <param name="size"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Adds an extension method to make string.Join more ergonomic
        /// </summary>
        /// <param name="strings"></param>
        /// <returns></returns>
        public static string Join(this IEnumerable<string> strings) => strings.Join("");

        /// <summary>
        /// Adds an extension method to make string.Join more ergonomic
        /// </summary>
        /// <param name="strings"></param>
        /// <param name="separator"></param>
        /// <returns></returns>
        public static string Join(this IEnumerable<string> strings, string separator) => string.Join(separator, strings);
    }
}
