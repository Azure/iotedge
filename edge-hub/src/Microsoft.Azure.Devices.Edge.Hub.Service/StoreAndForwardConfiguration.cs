// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.IO;
    using Microsoft.Extensions.Configuration;

    public class StoreAndForwardConfiguration
    {
        const int DefaultStorageSizeMb = 100;

        public StoreAndForwardConfiguration(bool isEnabled, string storagePath, TimeSpan timeToLive, int maxStorageSizeMb)
        {
            this.IsEnabled = isEnabled;
            this.StoragePath = storagePath;
            this.TimeToLive = timeToLive;
            this.MaxStorageSizeMb = maxStorageSizeMb;
        }

        public static StoreAndForwardConfiguration Initialize(IConfiguration configuration)
        {
            IConfiguration storeAndForwardConfiguration = configuration.GetSection("storeAndForward");
            bool isEnabled = storeAndForwardConfiguration.GetValue<bool>("enabled");
            if (isEnabled)
            {
                string storageFolder = storeAndForwardConfiguration.GetValue<string>("storageFolder");
                if (string.IsNullOrWhiteSpace(storageFolder))
                {
                    storageFolder = Path.Combine(Path.GetTempPath(), "edgeHubStorage");
                }              

                int timeToLiveSecs = configuration.GetValue<int>("timeToLiveSecs", -1);
                TimeSpan timeToLive = timeToLiveSecs < 0 ? TimeSpan.MaxValue : TimeSpan.FromSeconds(timeToLiveSecs);

                int maxStorageSizeMb = configuration.GetValue<int>("maxStorageSizeMb", DefaultStorageSizeMb);
                return new StoreAndForwardConfiguration(isEnabled, storageFolder, timeToLive, maxStorageSizeMb);
            }
            else
            {
                return new StoreAndForwardConfiguration(false, null, TimeSpan.MaxValue, 0);
            }
        }

        public bool IsEnabled { get; }

        public string StoragePath { get; }

        public TimeSpan TimeToLive { get; }

        public int MaxStorageSizeMb { get; }
    }
}
