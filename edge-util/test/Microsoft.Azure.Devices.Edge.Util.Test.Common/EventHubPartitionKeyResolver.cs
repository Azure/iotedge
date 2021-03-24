// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Devices.Common;

    public static class EventHubPartitionKeyResolver
    {
        private const short DefaultLogicalPartitionCount = short.MaxValue;

        public static string ResolveToPartition(string partitionKey, int partitionCount)
        {
            if (string.IsNullOrWhiteSpace(partitionKey))
            {
                throw new ArgumentNullException(nameof(partitionKey));
            }

            if (partitionCount < 1 || partitionCount > DefaultLogicalPartitionCount)
            {
                throw new ArgumentOutOfRangeException(nameof(partitionCount), partitionCount, string.Format(CultureInfo.InvariantCulture, "Should be between {0} and {1}", 1, DefaultLogicalPartitionCount));
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
}
