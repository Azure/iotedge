// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using Microsoft.Extensions.Configuration;

    public class StorageSpaceCheckConfiguration
    {
        const long DefaultMaxStorageBytes = long.MaxValue;
        const long DefaultCheckFrequencySecs = 600;

        public StorageSpaceCheckConfiguration(bool enabled)
            : this(enabled, TimeSpan.FromSeconds(DefaultCheckFrequencySecs), DefaultMaxStorageBytes)
        {
        }

        public StorageSpaceCheckConfiguration(bool enabled, TimeSpan checkFrequency, long maxStorageBytes)
        {
            Enabled = enabled;
            CheckFrequency = checkFrequency;
            MaxStorageBytes = maxStorageBytes;
        }

        public static StorageSpaceCheckConfiguration Create(bool enabled, IConfiguration storageSpaceCheckConfiguration)
        {
            enabled = enabled && storageSpaceCheckConfiguration.GetValue("enabled", false);
            long checkFrequencySecs = storageSpaceCheckConfiguration.GetValue("checkFrequencySecs", DefaultCheckFrequencySecs);
            TimeSpan checkFrequency = TimeSpan.FromSeconds(checkFrequencySecs);
            long maxStorageBytes = storageSpaceCheckConfiguration.GetValue("maxStorageBytes", DefaultMaxStorageBytes);
            return new StorageSpaceCheckConfiguration(enabled, checkFrequency, maxStorageBytes);
        }

        public bool Enabled { get; }

        public TimeSpan CheckFrequency { get; }

        public long MaxStorageBytes { get; }
    }
}
