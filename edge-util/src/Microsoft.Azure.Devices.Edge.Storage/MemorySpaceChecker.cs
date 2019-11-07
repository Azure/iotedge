// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Provides functionality to check whether total memory space consumption hasn't exceeded configured limits.
    /// NOTE: The methods in this class aren't thread safe. The expectation is for callers to employ thread-safe
    /// practices when using methods of this class.
    /// </summary>
    public class MemorySpaceChecker : IStorageSpaceChecker
    {
        internal enum MemoryUsageStatus
        {
            Unknown = 0,
            Available,
            Critical,
            Full
        }

        long maxSize;
        bool isEnabled;
        Func<long> getTotalMemoryUsage;
        MemoryUsageStatus memoryUsageStatus;

        public MemorySpaceChecker(Func<long> getTotalMemoryUsage)
        {
            this.getTotalMemoryUsage = Preconditions.CheckNotNull(getTotalMemoryUsage, nameof(getTotalMemoryUsage));
            this.maxSize = long.MaxValue;
            this.memoryUsageStatus = MemoryUsageStatus.Unknown;
        }

        public void SetMaxSizeBytes(Option<long> maxSizeBytes)
        {
            if (maxSizeBytes.HasValue)
            {
                this.isEnabled = true;
                maxSizeBytes.ForEach(x =>
                {
                    this.maxSize = x;
                    Events.SetMaxMemorySpaceUsage(x);
                });
            }
            else
            {
                this.isEnabled = false;
                this.memoryUsageStatus = MemoryUsageStatus.Unknown;
                Events.Disabled();
                this.maxSize = long.MaxValue;
            }
        }

        public Func<long> GetTotalMemoryUsage
        {
            get
            {
                return this.getTotalMemoryUsage;
            }
            private set
            {
                Preconditions.CheckNotNull(value, nameof(value));
                this.getTotalMemoryUsage = value;
            }
        }

        public bool IsFull => this.isEnabled && this.GetMemoryUsageStatus() == MemoryUsageStatus.Full;

        internal MemoryUsageStatus UsageStatus
        {
            get { return this.memoryUsageStatus; }
        }

        public void SetStorageUsageComputer(Func<long> storageUsageComputer) => this.GetTotalMemoryUsage = storageUsageComputer;

        MemoryUsageStatus GetMemoryUsageStatus()
        {
            long memoryUsageBytes = this.GetTotalMemoryUsage();
            double usagePercentage = (this.maxSize <= 1) ? 100 : (double)memoryUsageBytes * 100 / this.maxSize;
            Events.MemoryUsageStats(memoryUsageBytes, usagePercentage, this.maxSize);

            MemoryUsageStatus memoryUsageStatus = GetMemoryUsageStatus(usagePercentage);
            if (memoryUsageStatus != MemoryUsageStatus.Available)
            {
                Events.HighMemoryUsageDetected(usagePercentage, this.maxSize);
            }

            this.memoryUsageStatus = memoryUsageStatus;
            return memoryUsageStatus;
        }

        static MemoryUsageStatus GetMemoryUsageStatus(double usagePercentage)
        {
            if (usagePercentage < 90)
            {
                return MemoryUsageStatus.Available;
            }

            if (usagePercentage < 100)
            {
                return MemoryUsageStatus.Critical;
            }

            return MemoryUsageStatus.Full;
        }

        static class Events
        {
            public static readonly ILogger Log = Logger.Factory.CreateLogger<MemorySpaceChecker>();
            const int IdStart = UtilEventsIds.MemorySpaceChecker;

            enum EventIds
            {
                Disabled = IdStart,
                HighMemoryUsageDetected,
                MemoryUsageStats,
                SetMaxMemorySpaceUsage,
            }

            public static void HighMemoryUsageDetected(double usagePercentage, long maxSizeBytes)
            {
                Log.LogWarning((int)EventIds.HighMemoryUsageDetected, $"High memory usage detected - using {usagePercentage}% of {maxSizeBytes} bytes");
            }

            public static void MemoryUsageStats(long consumedMemoryBytes, double usagePercentage, long maxSizeBytes)
            {
                Log.LogDebug((int)EventIds.MemoryUsageStats, $"Memory usage - Consuming {consumedMemoryBytes} bytes using {usagePercentage}% of {maxSizeBytes} bytes");
            }

            public static void SetMaxMemorySpaceUsage(long maxSizeBytes)
            {
                Log.LogInformation((int)EventIds.SetMaxMemorySpaceUsage, $"Setting maximum memory space usage to {maxSizeBytes} bytes.");
            }

            public static void Disabled()
            {
                Log.LogInformation((int)EventIds.Disabled, "The memory space checker has been disabled.");
            }
        }
    }
}
