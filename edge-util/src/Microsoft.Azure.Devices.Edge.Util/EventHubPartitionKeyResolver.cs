// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Globalization;
    using System.Text;

    public static class EventHubPartitionKeyResolver
    {
        const short DefaultLogicalPartitionCount = short.MaxValue;

        public static string ResolveToPartition(string partitionKey, int partitionCount)
        {
            if (string.IsNullOrWhiteSpace(partitionKey))
            {
                throw new ArgumentNullException("partitionKey");
            }

            if (partitionCount < 1 || partitionCount > DefaultLogicalPartitionCount)
            {
                throw new ArgumentOutOfRangeException("partitionCount", partitionCount, string.Format(CultureInfo.InvariantCulture, "Should be between {0} and {1}", 1, DefaultLogicalPartitionCount));
            }

            short logicalPartition = Math.Abs((short)(PerfectHash.HashToShort(partitionKey) % DefaultLogicalPartitionCount));

            int shortRangeWidth = (int)Math.Floor((decimal)DefaultLogicalPartitionCount / (decimal)partitionCount);
            int remainingLogicalPartitions = DefaultLogicalPartitionCount - (partitionCount * shortRangeWidth);
            int largeRangeWidth = shortRangeWidth + 1;
            int largeRangesLogicalPartitions = largeRangeWidth * remainingLogicalPartitions;
            int partitionIndex = logicalPartition < largeRangesLogicalPartitions
                ? logicalPartition / largeRangeWidth
                : remainingLogicalPartitions + ((logicalPartition - largeRangesLogicalPartitions) / shortRangeWidth);

            return partitionIndex.ToString(NumberFormatInfo.InvariantInfo);
        }
    }

    public static class PerfectHash
    {
        public static long HashToLong(string data)
        {
            uint hash1;
            uint hash2;

#if NETSTANDARD1_3
            string upper = data.ToUpper();
#else
            string upper = data.ToUpper(CultureInfo.InvariantCulture);
#endif
            PerfectHash.ComputeHash(ASCIIEncoding.ASCII.GetBytes(upper), seed1: 0, seed2: 0, hash1: out hash1, hash2: out hash2);
            long hashedValue = ((long)hash1 << 32) | (long)hash2;

            return hashedValue;
        }

        public static short HashToShort(string data)
        {
            uint hash1;
            uint hash2;

#if NETSTANDARD1_3
            string upper = data.ToUpper();
#else
            string upper = data.ToUpper(CultureInfo.InvariantCulture);
#endif
            PerfectHash.ComputeHash(ASCIIEncoding.ASCII.GetBytes(upper), seed1: 0, seed2: 0, hash1: out hash1, hash2: out hash2);
            long hashedValue = hash1 ^ hash2;

            return (short)hashedValue;
        }

        // Perfect hashing implementation. source: distributed cache team
        static void ComputeHash(byte[] data, uint seed1, uint seed2, out uint hash1, out uint hash2)
        {
            uint a, b, c;

            a = b = c = (uint)(0xdeadbeef + data.Length + seed1);
            c += seed2;

            int index = 0, size = data.Length;
            while (size > 12)
            {
                a += BitConverter.ToUInt32(data, index);
                b += BitConverter.ToUInt32(data, index + 4);
                c += BitConverter.ToUInt32(data, index + 8);

                a -= c;
                a ^= (c << 4) | (c >> 28);
                c += b;

                b -= a;
                b ^= (a << 6) | (a >> 26);
                a += c;

                c -= b;
                c ^= (b << 8) | (b >> 24);
                b += a;

                a -= c;
                a ^= (c << 16) | (c >> 16);
                c += b;

                b -= a;
                b ^= (a << 19) | (a >> 13);
                a += c;

                c -= b;
                c ^= (b << 4) | (b >> 28);
                b += a;

                index += 12;
                size -= 12;
            }

            switch (size)
            {
                case 12:
                    a += BitConverter.ToUInt32(data, index);
                    b += BitConverter.ToUInt32(data, index + 4);
                    c += BitConverter.ToUInt32(data, index + 8);
                    break;
                case 11:
                    c += ((uint)data[index + 10]) << 16;
                    goto case 10;
                case 10:
                    c += ((uint)data[index + 9]) << 8;
                    goto case 9;
                case 9:
                    c += (uint)data[index + 8];
                    goto case 8;
                case 8:
                    b += BitConverter.ToUInt32(data, index + 4);
                    a += BitConverter.ToUInt32(data, index);
                    break;
                case 7:
                    b += ((uint)data[index + 6]) << 16;
                    goto case 6;
                case 6:
                    b += ((uint)data[index + 5]) << 8;
                    goto case 5;
                case 5:
                    b += (uint)data[index + 4];
                    goto case 4;
                case 4:
                    a += BitConverter.ToUInt32(data, index);
                    break;
                case 3:
                    a += ((uint)data[index + 2]) << 16;
                    goto case 2;
                case 2:
                    a += ((uint)data[index + 1]) << 8;
                    goto case 1;
                case 1:
                    a += (uint)data[index];
                    break;
                case 0:
                    hash1 = c;
                    hash2 = b;
                    return;
            }

            c ^= b;
            c -= (b << 14) | (b >> 18);

            a ^= c;
            a -= (c << 11) | (c >> 21);

            b ^= a;
            b -= (a << 25) | (a >> 7);

            c ^= b;
            c -= (b << 16) | (b >> 16);

            a ^= c;
            a -= (c << 4) | (c >> 28);

            b ^= a;
            b -= (a << 14) | (a >> 18);

            c ^= b;
            c -= (b << 24) | (b >> 8);

            hash1 = c;
            hash2 = b;
        }
    }
}
