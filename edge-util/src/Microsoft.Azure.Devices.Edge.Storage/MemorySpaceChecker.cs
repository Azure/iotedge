// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class MemorySpaceChecker : IStorageSpaceChecker
    {
        internal enum MemoryUsageStatus
        {
            Unknown = 0,
            Available,
            Critical,
            Full
        }

        readonly PeriodicTask storageSpaceChecker;
        long maxStorageSpaceBytes;
        Func<Task<long>> getTotalMemoryUsage;
        MemoryUsageStatus memoryUsageStatus;

        public MemorySpaceChecker(TimeSpan checkFrequency, long maxStorageSpaceBytes, Func<Task<long>> getTotalMemoryUsage)
        {
            Preconditions.CheckNotNull(getTotalMemoryUsage, nameof(getTotalMemoryUsage));
            this.maxStorageSpaceBytes = maxStorageSpaceBytes;
            this.getTotalMemoryUsage = getTotalMemoryUsage;
            this.storageSpaceChecker = new PeriodicTask(this.PeriodicTaskCallback, checkFrequency, checkFrequency, Events.Log, "Memory usage check");
        }

        public Func<Task<long>> GetTotalMemoryUsage
        {
            get
            {
                return this.getTotalMemoryUsage;
            }
            set
            {
                Preconditions.CheckNotNull(value, nameof(value));
                this.getTotalMemoryUsage = value;
            }
        }

        public bool IsFull => this.memoryUsageStatus == MemoryUsageStatus.Full;

        internal MemoryUsageStatus UsageStatus
        {
            get { return this.memoryUsageStatus; }
        }

        public void SetStorageUsageComputer(Func<Task<long>> storageUsageComputer) => this.GetTotalMemoryUsage = storageUsageComputer;

        public void SetMaxStorageSize(long maxStorageBytes)
        {
            Events.SetMaxMemorySpaceUsage(maxStorageBytes);
            this.maxStorageSpaceBytes = maxStorageBytes;
        }

        async Task PeriodicTaskCallback()
        {
            try
            {
                this.memoryUsageStatus = await this.GetMemoryUsageStatus();
            }
            catch (Exception e)
            {
                Events.ErrorGettingMemoryUsageStatus(e);
            }
        }

        async Task<MemoryUsageStatus> GetMemoryUsageStatus()
        {
            long memoryUsageBytes = await this.GetTotalMemoryUsage();
            double usagePercentage = (double)memoryUsageBytes * 100 / this.maxStorageSpaceBytes;
            Events.MemoryUsageStats(memoryUsageBytes, usagePercentage, this.maxStorageSpaceBytes);

            MemoryUsageStatus memoryUsageStatus = GetMemoryUsageStatus(usagePercentage);
            if (memoryUsageStatus != MemoryUsageStatus.Available)
            {
                Events.HighMemoryUsageDetected(usagePercentage, this.maxStorageSpaceBytes);
            }

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
                Created = IdStart,
                ErrorGetMemoryUsageStatus,
                HighMemoryUsageDetected,
                MemoryUsageStats,
                SetMaxMemorySpaceUsage
            }

            public static void Created()
            {
                Log.LogInformation((int)EventIds.Created, "Created maximum memory space usage checker.");
            }

            public static void ErrorGettingMemoryUsageStatus(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorGetMemoryUsageStatus, ex, "Error occurred while getting memory usage status.");
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
        }
    }
}
