// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ReportingCacheWithStorage
    {
        Storage storage;

        public static ReportingCacheWithStorage Instance { get; } = new ReportingCacheWithStorage();

        ReportingCacheWithStorage()
        {
        }

        public async Task InitAsync(string storagePath, bool optimizeForPerformance)
        {
            this.storage = new Storage();
            await this.storage.InitAsync(storagePath, new SystemEnvironment(), optimizeForPerformance);
            await this.storage.ProcessAllMessagesAsync(message => ReportingCache.Instance.AddMessage(message));
            await this.storage.ProcessAllDirectMethodsAsync(directMethodStatus => ReportingCache.Instance.AddDirectMethodStatus(directMethodStatus));
            await this.storage.ProcessAllTwinsAsync(twinStatus => ReportingCache.Instance.AddTwinStatus(twinStatus));
        }

        public async Task AddMessageAsync(MessageDetails msg)
        {
            bool added = await this.storage.AddMessageAsync(msg);
            if (added)
            {
                ReportingCache.Instance.AddMessage(msg);
            }
        }

        public async Task AddDirectMethodAsync(ResponseStatus dmStatus)
        {
            bool added = await this.storage.AddDirectMethodAsync(dmStatus);
            if (added)
            {
                ReportingCache.Instance.AddDirectMethodStatus(dmStatus);
            }
        }

        public async Task AddTwinAsync(ResponseStatus twinStatus)
        {
            bool added = await this.storage.AddTwinAsync(twinStatus);
            if (added)
            {
                ReportingCache.Instance.AddTwinStatus(twinStatus);
            }
        }
    }
}
