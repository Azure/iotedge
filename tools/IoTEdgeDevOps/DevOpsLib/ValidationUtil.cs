// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class ValidationUtil
    {
        public static void ThrowIfNullOrWhiteSpace(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Cannot be null or white space.", field);
            }
        }

        public static void ThrowIfNonPositive(int value, string field)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(field, "Cannot be less than or equal to zero.");
            }
        }

        public static void ThrowIfNulOrEmptySet<T>(IEnumerable<T> value, string field)
        {
            if (value == null || !value.Any())
            {
                throw new ArgumentException("Cannot be null or empty collection.", field);
            }
        }

        public static void ThrowIfNull(object value, string field)
        {
            if (value == null)
            {
                throw new ArgumentNullException("Cannot be null.", field);
            }
        }
    }
}
