// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class MemorySpaceChecker : IStorageSpaceChecker
    {
        static readonly long DefaultCheckFrequency = 120;

        internal enum MemoryUsageStatus
        {
            Unknown = 0,
            Available,
            Critical,
            Full
        }

        readonly object updateLock = new object();

        PeriodicTask storageSpaceChecker;
        long maxSize;
        long checkFrequency;
        Func<Task<long>> getTotalMemoryUsage;
        MemoryUsageStatus memoryUsageStatus;

        public MemorySpaceChecker(Func<Task<long>> getTotalMemoryUsage)
        {
            this.getTotalMemoryUsage = Preconditions.CheckNotNull(getTotalMemoryUsage, nameof(getTotalMemoryUsage));
            this.maxSize = long.MaxValue;
            this.memoryUsageStatus = MemoryUsageStatus.Unknown;
            this.checkFrequency = long.MaxValue;
        }

        public MemorySpaceChecker(TimeSpan checkFrequency, long maxSize, Func<Task<long>> getTotalMemoryUsage)
            : this(getTotalMemoryUsage)
        {
            this.maxSize = maxSize;
            this.storageSpaceChecker = new PeriodicTask(this.PeriodicTaskCallback, checkFrequency, checkFrequency, Events.Log, "Memory usage check");
        }

        public void SetMaxSize(long maxSize, Option<long> newCheckFrequency)
        {
            this.maxSize = maxSize;
            long updatedCheckFrequency = newCheckFrequency.GetOrElse(long.MinValue);

            TimeSpan frequency = default;
            if (this.checkFrequency != updatedCheckFrequency)
            {
                lock (this.updateLock)
                {
                    this.checkFrequency = updatedCheckFrequency;
                    frequency = TimeSpan.FromSeconds(this.checkFrequency < 0 ? DefaultCheckFrequency : this.checkFrequency);

                    this.storageSpaceChecker?.Dispose();
                    this.storageSpaceChecker = new PeriodicTask(this.PeriodicTaskCallback, frequency, frequency, Events.Log, "Memory usage check");
                }
            }

            Events.SetMaxMemorySpaceUsage(maxSize, this.checkFrequency < 0 ? DefaultCheckFrequency : this.checkFrequency);
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
            double usagePercentage = (double)memoryUsageBytes * 100 / this.maxSize;
            Events.MemoryUsageStats(memoryUsageBytes, usagePercentage, this.maxSize);

            MemoryUsageStatus memoryUsageStatus = GetMemoryUsageStatus(usagePercentage);
            if (memoryUsageStatus != MemoryUsageStatus.Available)
            {
                Events.HighMemoryUsageDetected(usagePercentage, this.maxSize);
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

            public static void SetMaxMemorySpaceUsage(long maxSizeBytes, long checkFrequency)
            {
                Log.LogInformation((int)EventIds.SetMaxMemorySpaceUsage, $"Setting maximum memory space usage to {maxSizeBytes} bytes with a check frequency of {checkFrequency} seconds.");
            }
        }
    }
}
