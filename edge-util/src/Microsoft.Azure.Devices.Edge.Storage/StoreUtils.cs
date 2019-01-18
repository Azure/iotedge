// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

    public static class StoreUtils
    {
        public static long GetOffsetFromKey(byte[] key)
        {
            Preconditions.CheckNotNull(key, nameof(key));
            if (BitConverter.IsLittleEndian)
            {
                key = key.ToArray();
                Array.Reverse(key);
            }

            long offset = BitConverter.ToInt64(key, 0);
            return offset;
        }

        public static byte[] GetKeyFromOffset(long offset)
        {
            Preconditions.CheckRange(offset, 0, nameof(offset));
            byte[] bytes = BitConverter.GetBytes(offset);
            if (BitConverter.IsLittleEndian)
            {
                bytes = bytes.ToArray();
                Array.Reverse(bytes);
            }

            return bytes;
        }

        public static IDictionary<string, string> ToDictionary(this IReadOnlyDictionary<string, string> readOnlyDictionary)
        {
            Preconditions.CheckNotNull(readOnlyDictionary, nameof(readOnlyDictionary));
            if (!(readOnlyDictionary is IDictionary<string, string> properties))
            {
                properties = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> item in readOnlyDictionary)
                {
                    properties.Add(item.Key, item.Value);
                }
            }

            return properties;
        }
    }
}
